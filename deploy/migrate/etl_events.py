#!/usr/bin/env python3
"""
Events:  social_db.social_media_event(+participants)  ->  innovator_events.*

CreatedByUsername is filled from social_media_user. Participant ids are bigint
in the old DB, so new UUIDs are generated for EventParticipants.

Run:  python3 etl_events.py --dry-run    then    python3 etl_events.py
"""

import uuid
from datetime import datetime, timezone
import psycopg2.extras
from mig_common import connect, rows, existing_ids, dry_run_flag, SRC_SOCIAL, DST_EVENTS

DRY = dry_run_flag()
NOW = datetime.now(timezone.utc)


def insert(dst, table, columns, data):
    if not data:
        print(f"  {table:18} nothing to insert")
        return
    placeholders = ",".join(["%s"] * len(columns))
    collist = ",".join(f'"{c}"' for c in columns)
    sql = f'INSERT INTO public."{table}" ({collist}) VALUES ({placeholders});'
    if not DRY:
        with dst.cursor() as cur:
            psycopg2.extras.execute_batch(cur, sql, data, page_size=500)
    print(f"  {table:18} {'would insert' if DRY else 'inserted'}: {len(data)}")


def main():
    src = connect(SRC_SOCIAL)
    dst = connect(DST_EVENTS)

    users = {r["id"]: r["username"] for r in rows(src, "SELECT id, username FROM social_media_user;")}

    have_events = existing_ids(dst, 'public."Events"')
    events = [e for e in rows(src, """SELECT id, title, description, location, date,
                                             created_at, updated_at, created_by_id
                                      FROM social_media_event;""")
              if e["id"] not in have_events]
    migrated_event_ids = set(have_events) | {e["id"] for e in events}

    insert(dst, "Events",
           ["Id", "Title", "Description", "Location", "Date", "CreatedById",
            "CreatedByUsername", "CreatedAt", "UpdatedAt"],
           [(str(e["id"]), e["title"] or "", e["description"] or "", e["location"] or "",
             e["date"] or NOW, str(e["created_by_id"]) if e["created_by_id"] else None,
             users.get(e["created_by_id"], "") or "",
             e["created_at"] or NOW, e["updated_at"] or e["created_at"] or NOW)
            for e in events])

    parts = [p for p in rows(src, "SELECT event_id, user_id FROM social_media_event_participants;")
             if p["event_id"] in migrated_event_ids]
    insert(dst, "EventParticipants", ["Id", "EventId", "UserId", "CreatedAt", "UpdatedAt"],
           [(str(uuid.uuid4()), str(p["event_id"]), str(p["user_id"]), NOW, NOW) for p in parts])

    if not DRY:
        dst.commit()
    src.close(); dst.close()
    print("Done." if not DRY else "Dry run complete (nothing written).")


if __name__ == "__main__":
    main()
