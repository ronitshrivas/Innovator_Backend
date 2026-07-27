#!/usr/bin/env python3
"""
Feed (social):  social_db.*  ->  innovator_feed.*

Migrates categories, posts, post media, post<->category links, reactions and
comments. REELS ARE EXCLUDED: the social_media_reel table is ignored, and any
reaction/comment whose reel_id is set (or whose post_id is null) is skipped.

Username/avatar are denormalised onto posts/comments in the new schema, so we
join social_media_user + social_media_profile to fill them.

Self-referencing links (post shares, comment replies) are set in a second pass
so the parent row always exists first.

Run:  python3 etl_feed.py --dry-run    then    python3 etl_feed.py
"""

from datetime import datetime, timezone
import psycopg2.extras
from mig_common import connect, rows, existing_ids, dry_run_flag, SRC_SOCIAL, DST_FEED

DRY = dry_run_flag()
NOW = datetime.now(timezone.utc)


def insert(dst, table, columns, data):
    if not data:
        print(f"  {table:16} nothing to insert")
        return
    placeholders = ",".join(["%s"] * len(columns))
    collist = ",".join(f'"{c}"' for c in columns)
    sql = f'INSERT INTO public."{table}" ({collist}) VALUES ({placeholders});'
    if not DRY:
        with dst.cursor() as cur:
            psycopg2.extras.execute_batch(cur, sql, data, page_size=500)
    print(f"  {table:16} {'would insert' if DRY else 'inserted'}: {len(data)}")


def main():
    src = connect(SRC_SOCIAL)
    dst = connect(DST_FEED)

    users = {r["id"]: r["username"] for r in rows(src, "SELECT id, username FROM social_media_user;")}
    avatars = {r["user_id"]: r["avatar"] for r in rows(src, "SELECT user_id, avatar FROM social_media_profile;")}

    def uname(uid): return users.get(uid, "")
    def avatar(uid): return avatars.get(uid)

    # ---- Categories ----
    have = existing_ids(dst, 'public."Categories"')
    cats = [c for c in rows(src, "SELECT id, name, description, created_at FROM social_media_category;")
            if c["id"] not in have]
    insert(dst, "Categories", ["Id", "Name", "Description", "CreatedAt", "UpdatedAt"],
           [(str(c["id"]), c["name"], c["description"], c["created_at"] or NOW, c["created_at"] or NOW) for c in cats])

    # ---- Posts (pass 1: no SharedPostId) ----
    have_posts = existing_ids(dst, 'public."Posts"')
    src_posts = rows(src, """SELECT id, user_id, content, views_count, shared_post_id, created_at, updated_at
                             FROM social_media_post;""")
    migrated_post_ids = set(have_posts)
    new_posts = [p for p in src_posts if p["id"] not in have_posts]
    insert(dst, "Posts",
           ["Id", "AuthorId", "Username", "Avatar", "Content", "Type", "IsReel",
            "ViewsCount", "SharedPostId", "CreatedAt", "UpdatedAt"],
           [(str(p["id"]), str(p["user_id"]) if p["user_id"] else None,
             uname(p["user_id"]) or "", avatar(p["user_id"]) or "", p["content"] or "",
             "post", False, p["views_count"] or 0, None,
             p["created_at"] or NOW, p["updated_at"] or p["created_at"] or NOW)
            for p in new_posts])
    for p in new_posts:
        migrated_post_ids.add(p["id"])

    # ---- Posts (pass 2: set SharedPostId only when target exists) ----
    if not DRY:
        with dst.cursor() as cur:
            for p in new_posts:
                sp = p["shared_post_id"]
                if sp and sp in migrated_post_ids:
                    cur.execute('UPDATE public."Posts" SET "SharedPostId"=%s WHERE "Id"=%s;',
                                (str(sp), str(p["id"])))

    # ---- Post media (posts only) ----
    have_media = existing_ids(dst, 'public."PostMedia"')
    media = [m for m in rows(src, "SELECT id, file, media_type, post_id FROM social_media_postmedia;")
             if m["id"] not in have_media and m["post_id"] in migrated_post_ids]
    insert(dst, "PostMedia", ["Id", "PostId", "File", "MediaType", "Thumbnail", "CreatedAt", "UpdatedAt"],
           [(str(m["id"]), str(m["post_id"]), m["file"] or "", m["media_type"] or "image", None, NOW, NOW)
            for m in media])

    # ---- Post <-> Category links (composite key, no Id) ----
    have_links = set()
    if not DRY:
        with dst.cursor() as cur:
            cur.execute('SELECT "PostId","CategoryId" FROM public."PostCategories";')
            have_links = {(r[0], r[1]) for r in cur.fetchall()}
    links = []
    for r in rows(src, "SELECT post_id, category_id FROM social_media_post_categories;"):
        if r["post_id"] in migrated_post_ids and (r["post_id"], r["category_id"]) not in have_links:
            links.append((str(r["post_id"]), str(r["category_id"])))
    insert(dst, "PostCategories", ["PostId", "CategoryId"], links)

    # ---- Reactions (skip reel reactions) ----
    have_react = existing_ids(dst, 'public."Reactions"')
    reacts = [r for r in rows(src, "SELECT id, type, created_at, post_id, user_id, reel_id FROM social_media_reaction;")
              if r["reel_id"] is None and r["post_id"] in migrated_post_ids and r["id"] not in have_react]
    insert(dst, "Reactions", ["Id", "PostId", "AuthorId", "Type", "CreatedAt", "UpdatedAt"],
           [(str(r["id"]), str(r["post_id"]), str(r["user_id"]) if r["user_id"] else None,
             r["type"] or "like", r["created_at"] or NOW, r["created_at"] or NOW) for r in reacts])

    # ---- Comments (skip reel comments; pass 1 without ParentId) ----
    have_comments = existing_ids(dst, 'public."Comments"')
    src_comments = [c for c in rows(src, """SELECT id, content, created_at, parent_id, user_id, post_id, reel_id
                                            FROM social_media_comment;""")
                    if c["reel_id"] is None and c["post_id"] in migrated_post_ids and c["id"] not in have_comments]
    migrated_comment_ids = set(have_comments) | {c["id"] for c in src_comments}
    insert(dst, "Comments",
           ["Id", "PostId", "AuthorId", "Username", "Avatar", "Content", "ParentId", "CreatedAt", "UpdatedAt"],
           [(str(c["id"]), str(c["post_id"]), str(c["user_id"]) if c["user_id"] else None,
             uname(c["user_id"]) or "", avatar(c["user_id"]), c["content"] or "", None,
             c["created_at"] or NOW, c["created_at"] or NOW) for c in src_comments])

    # ---- Comments (pass 2: set ParentId when parent was migrated) ----
    if not DRY:
        with dst.cursor() as cur:
            for c in src_comments:
                pid = c["parent_id"]
                if pid and pid in migrated_comment_ids:
                    cur.execute('UPDATE public."Comments" SET "ParentId"=%s WHERE "Id"=%s;',
                                (str(pid), str(c["id"])))

    if not DRY:
        dst.commit()
    src.close(); dst.close()
    print("Done." if not DRY else "Dry run complete (nothing written).")


if __name__ == "__main__":
    main()
