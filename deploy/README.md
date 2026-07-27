# Innovator Backend — Deploy & Migrate (Ubuntu VM)

This guide takes you from the current VM (old Django backend) to the new .NET
backend running in Docker, with the old data migrated in, and the Flutter app
still working on the same IP and ports.

`HOST` below = `36.253.137.34`.

---

## 0. Golden rule: back up first

SSH in and dump **everything** before you change anything. If the old backend
uses Postgres, this snapshot is your safety net.

```bash
sudo -u postgres pg_dumpall > ~/old_backend_full_backup_$(date +%F).sql
ls -lh ~/old_backend_full_backup_*.sql          # confirm it's not empty
```

Keep a copy off the VM too (`scp` it to your laptop).

---

## 1. Install Docker

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
  https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker $USER      # then log out & back in so `docker` works without sudo
docker --version && docker compose version
```

---

## 2. Get the code onto the VM

Either `git clone` your repo, or from your laptop:

```bash
scp -r /path/to/InnovatorBackend ubuntu@36.253.137.34:~/InnovatorBackend
```

Then on the VM:

```bash
cd ~/InnovatorBackend
```

---

## 3. Inspect the old database (so we know what we're migrating)

```bash
chmod +x deploy/inspect_old_db.sh
# The old Django Postgres is normally on localhost:5432. If it needs a password:
PGUSER=postgres PGPASSWORD='THE_OLD_PASSWORD' ./deploy/inspect_old_db.sh | tee ~/old_schema.txt
```

Read `~/old_schema.txt`: note the users table (often `auth_user`), the products,
courses, orders, research papers and events tables, and their columns. You'll
plug these names into the ETL config in step 6.

---

## 4. Start the new stack

The new Postgres is published on host **5433** on purpose, so it runs alongside
the old Postgres (5432) while you migrate. The app-facing service ports
(8003/8004/8005/8010) are still used by the **old** backend right now — that's
fine, because the new services publish them too and Docker will simply fail to
bind if they're taken. So bring the new stack up **after** stopping the old one
(next step) — or bring it up now and only free the ports at cutover.

Build and launch:

```bash
docker compose up -d --build          # first build takes a few minutes
docker compose ps                     # all should be "running"/"healthy"
docker compose logs -f auth-service   # watch one service boot; Ctrl-C to exit
```

Each service creates its own schema automatically on first boot (EF migrations /
EnsureCreated), so the empty databases from `postgres-init` get their tables.

Quick check (from the VM):

```bash
curl -s http://localhost:8010/swagger/v1/swagger.json | head -c 200   # auth
curl -s http://localhost:8016/swagger/index.html | head -c 200        # ecommerce admin
```

---

## 5. Cutover: free the old ports

Your old backend is Docker Compose in `~/innovator`, holding 8003/8004/8005/8010.
Stop **only the app containers** — keep the old DB containers running so the
migration can read from them:

```bash
# Stop the app + workers by container name (NOT the *_db containers):
docker stop \
  innovator-auth-service-1 \
  innovator-elearning-service-1 \
  innovator-ecommerce-service-1 \
  innovator-social-media-service-1 \
  innovator-kms-service-1 \
  innovator-social-media-celery-1 \
  innovator-social-media-celery-beat-1

# Confirm the *_db containers are still up (auth_db, ecommerce_db, social_media_db):
docker ps | grep _db
```

Now bring the new stack up so it can grab the freed app ports:

```bash
cd ~/InnovatorBackend
docker compose up -d
docker compose ps        # gateway on 8005/8000, auth on 8010, ecommerce 8004, elearning 8003
```

(If you brought the new stack up earlier and some services failed to bind
8003/8004/8005/8010, just `docker compose up -d` again after stopping the old
app containers — the restart policy will retry.)

After migration is verified, you can stop the old DB containers too:
`cd ~/innovator && docker compose down`.

---

## 6. Migrate the data

The ETL scripts are pre-filled for **your** old databases (discovered from the
running containers):

| Old data | Container | Host port | Old DB |
|----------|-----------|-----------|--------|
| auth     | innovator-auth_db-1         | 5432 | auth_db |
| ecommerce| innovator-ecommerce_db-1    | 5435 | ecommerce_db |
| social   | innovator-social_media_db-1 | 5436 | social_db |

They connect to the OLD Postgres on those host ports and write to the NEW
Postgres on host **5440**. All old ids are UUIDs, so ids and foreign keys are
preserved exactly. **E-learning and reels are intentionally NOT migrated.**

Install the driver and run everything (users first — order matters):

```bash
sudo apt-get install -y python3-pip
pip3 install psycopg2-binary

cd ~/InnovatorBackend/deploy/migrate
chmod +x run_all.sh

./run_all.sh --dry-run      # report only, writes nothing — check the counts
./run_all.sh                # perform the migration (safe to re-run; skips existing)
```

Or run individually:

```bash
python3 etl_users.py --dry-run     && python3 etl_users.py
python3 etl_ecommerce.py --dry-run && python3 etl_ecommerce.py
python3 etl_feed.py --dry-run      && python3 etl_feed.py     # reels excluded
python3 etl_events.py --dry-run    && python3 etl_events.py
```

What gets migrated:

- **users**  `accounts_user` → `Users` (id, username, email, Django password
  hash, role, verified/active flags). Passwords keep working: AuthService
  verifies the Django `pbkdf2_sha256` hash and upgrades it to BCrypt on first
  login.
- **ecommerce**  categories, products, product images, orders + items, payment
  QRs, FCM tokens, notifications.
- **feed**  categories, posts, post media, post↔category links, reactions,
  comments — **reels and reel-only reactions/comments are skipped**.
- **events**  events + participants.

Verify a real login afterwards via AuthService Swagger
(`http://36.253.137.34:8010/swagger` → `POST /api/auth/sso/login`) using a known
old account.

> Note: research has no old table (the old DBs contain no research data), so
> there is nothing to migrate there.

---

## 7. Firewall

Make sure the app-facing ports are open to the internet:

```bash
sudo ufw allow 8003,8004,8005,8010/tcp     # app
sudo ufw allow 8000,8016,8017,8018,8019/tcp # gateway + admin swagger (optional)
sudo ufw status
```

(If a cloud provider security group is in front of the VM, open the same ports there.)

---

## 8. Research domain note

The Flutter research module calls `https://api.meta-tronix.com` (HTTPS, its own
domain) rather than an IP:port. To serve it from this VM, point that DNS record
at `36.253.137.34` and put a TLS reverse proxy (Caddy or nginx) in front of the
research service on 8019. Ask me and I'll add a Caddy service to the compose
file that terminates HTTPS for `api.meta-tronix.com`.

---

## 9. Everyday operations

```bash
docker compose ps                 # status
docker compose logs -f <service>  # tail logs
docker compose restart <service>  # restart one
docker compose down               # stop all (DB data is kept in the pgdata volume)
docker compose up -d --build      # rebuild after code changes
```

## 10. Rollback

Nothing was destroyed: the old backend is stopped, not deleted.

```bash
docker compose down                       # stop the new stack
sudo systemctl start <old-services>       # bring the old backend back
```

Your `pg_dumpall` from step 0 restores the old database if ever needed.
