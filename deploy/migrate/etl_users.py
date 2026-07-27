#!/usr/bin/env python3
"""
Users:  auth_db.accounts_user  ->  innovator_auth."Users"

Preserves the UUID id (so every other table's user references still match) and
the Django pbkdf2 password hash (AuthService verifies it and upgrades to BCrypt
on first login). Usernames/emails are lowercased to match AuthService's login
lookup. Idempotent: users already present (by id) are skipped.

Run:  python3 etl_users.py --dry-run    then    python3 etl_users.py
"""

from mig_common import connect, rows, existing_ids, dry_run_flag, SRC_AUTH, DST_AUTH

INSERT = '''
INSERT INTO public."Users"
    ("Id","Username","Email","PasswordHash","Role","IsEmailVerified","IsActive","Phone","CreatedAt","UpdatedAt")
VALUES (%s,%s,%s,%s,%s,%s,%s,%s, now(), now());
'''


def main():
    dry = dry_run_flag()
    src = connect(SRC_AUTH)
    dst = connect(DST_AUTH)

    users = rows(src, """
        SELECT id, username, email, password, role,
               is_email_verified, is_active, phone_number
        FROM accounts_user;
    """)
    have = existing_ids(dst, 'public."Users"')
    print(f"old users: {len(users)} | already migrated: {len(have)}")

    inserted = skipped = 0
    seen_email, seen_username = set(), set()

    with dst.cursor() as cur:
        for u in users:
            uid = u["id"]
            email = (u["email"] or "").strip().lower()
            username = (u["username"] or "").strip().lower()
            pwd = u["password"] or ""
            role = (u["role"] or "innovator").strip() or "innovator"

            if uid in have or not email or not username or not pwd:
                skipped += 1
                continue
            if email in seen_email or username in seen_username:
                skipped += 1   # AuthService enforces unique email/username
                continue

            seen_email.add(email)
            seen_username.add(username)

            if dry:
                inserted += 1
                continue

            # Savepoint per row so a residual duplicate (e.g. same username in a
            # different case) is skipped instead of aborting the whole run.
            try:
                cur.execute("SAVEPOINT sp;")
                cur.execute(INSERT, (
                    str(uid), username, email, pwd, role,
                    bool(u["is_email_verified"]),
                    True if u["is_active"] is None else bool(u["is_active"]),
                    u["phone_number"],
                ))
                cur.execute("RELEASE SAVEPOINT sp;")
                inserted += 1
            except Exception as ex:
                cur.execute("ROLLBACK TO SAVEPOINT sp;")
                skipped += 1
                if skipped <= 6:
                    print(f"  ! skipped one user: {str(ex).splitlines()[0]}")

    if not dry:
        dst.commit()
    print(f"{'WOULD INSERT' if dry else 'inserted'}: {inserted} | skipped: {skipped}")
    src.close(); dst.close()


if __name__ == "__main__":
    main()
