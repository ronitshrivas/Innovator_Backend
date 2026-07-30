#!/usr/bin/env python3
"""
Backfill the SearchService user index from ProfileService.

Migrated users live in innovator_profile."UserProfiles" but were never pushed
into innovator_search."UserIndex", so search returns nothing. This copies every
profile into the search index. Idempotent (skips ones already indexed).

Run on the VM:
    python3 deploy/backfill_search.py
"""
import psycopg2

DB_HOST = "localhost"
DB_PORT = 5440
DB_USER = "innovator"
DB_PASS = "innovator123"

PROFILE_DB = "innovator_profile"
SEARCH_DB = "innovator_search"


def connect(db):
    return psycopg2.connect(
        host=DB_HOST, port=DB_PORT, user=DB_USER, password=DB_PASS, dbname=db
    )


def main():
    prof = connect(PROFILE_DB)
    search = connect(SEARCH_DB)
    pc = prof.cursor()
    sc = search.cursor()

    pc.execute('''
        SELECT "AuthUserId", "Username", "FullName", "AvatarPath", "Bio",
               "Role", "InterestsJson"
        FROM "UserProfiles"
    ''')
    profiles = pc.fetchall()
    print("Profiles found:", len(profiles))

    sc.execute('SELECT "AuthUserId" FROM "UserIndex"')
    existing = {r[0] for r in sc.fetchall()}
    print("Already indexed:", len(existing))

    n = 0
    for auth_id, username, full_name, avatar, bio, role, interests in profiles:
        if auth_id in existing:
            continue
        try:
            sc.execute('''
                INSERT INTO "UserIndex"
                    ("Id","AuthUserId","Username","FullName","Avatar","Bio",
                     "Role","InterestsJson","FollowersCount","FollowingCount",
                     "IsActive","CreatedAt","UpdatedAt")
                VALUES
                    (gen_random_uuid(),%s,%s,%s,%s,%s,%s,%s,0,0,true,now(),now())
            ''', (auth_id, username or "", full_name or "", avatar, bio,
                  role or "innovator", interests or "[]"))
            search.commit()
            n += 1
        except Exception as e:
            search.rollback()
            print("skip", username, e)

    print("Indexed", n, "new users.")
    prof.close()
    search.close()


if __name__ == "__main__":
    main()
