#!/usr/bin/env python3
"""
Backfill ProfileService from AuthService.

Every auth user must have a row in innovator_profile."UserProfiles" or the
profile screens 404. This copies auth_user_id / username / email / role for
every user that doesn't already have a profile. Safe to re-run (idempotent).

Run on the VM:
    python3 deploy/migrate_profiles.py
"""
import psycopg2

DB_HOST = "localhost"
DB_PORT = 5440          # host port mapped to the stack's Postgres
DB_USER = "innovator"
DB_PASS = "innovator123"

AUTH_DB = "innovator_auth"
PROFILE_DB = "innovator_profile"


def connect(db):
    return psycopg2.connect(
        host=DB_HOST, port=DB_PORT, user=DB_USER, password=DB_PASS, dbname=db
    )


def main():
    auth = connect(AUTH_DB)
    prof = connect(PROFILE_DB)
    auth_cur = auth.cursor()
    prof_cur = prof.cursor()

    # Pull every auth user.
    auth_cur.execute('SELECT "Id", "Username", "Email", "Role" FROM "Users"')
    users = auth_cur.fetchall()
    print(f"Auth users found: {len(users)}")

    # Existing profiles (by auth id) so we skip them.
    prof_cur.execute('SELECT "AuthUserId" FROM "UserProfiles"')
    existing = {row[0] for row in prof_cur.fetchall()}
    print(f"Existing profiles: {len(existing)}")

    inserted = 0
    for uid, username, email, role in users:
        if uid in existing:
            continue
        try:
            prof_cur.execute(
                '''
                INSERT INTO "UserProfiles"
                    ("Id", "AuthUserId", "Username", "FullName", "Email",
                     "Role", "InterestsJson", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    (gen_random_uuid(), %s, %s, %s, %s, %s, '[]', true, now(), now())
                ''',
                (uid, (username or "").lower(), username or "", email or "",
                 role or "innovator"),
            )
            prof.commit()
            inserted += 1
        except Exception as e:
            prof.rollback()
            print(f"  skip {username} ({uid}): {e}")

    print(f"Inserted {inserted} new profiles.")
    auth.close()
    prof.close()


if __name__ == "__main__":
    main()
