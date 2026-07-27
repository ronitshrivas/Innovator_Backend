"""
Shared connection config + helpers for all Innovator migration scripts.

Old data lives in the running Django Postgres containers, reachable on the VM host:
    auth       -> 127.0.0.1:5432  db=auth_db        (accounts_user, accounts_otpcode)
    ecommerce  -> 127.0.0.1:5435  db=ecommerce_db
    social     -> 127.0.0.1:5436  db=social_db
New .NET Postgres is published by docker-compose on 127.0.0.1:5440.

All primary keys in the old DB are UUIDs, so we copy ids as-is and every foreign
key still lines up in the new DB — no id remapping needed.
"""

import sys

try:
    import psycopg2
    import psycopg2.extras
except ImportError:
    sys.exit("psycopg2 is required:  pip3 install psycopg2-binary")

OLD_USER = "innovator_user"
OLD_PASSWORD = "Nep@tronix9335%"
OLD_HOST = "127.0.0.1"

# ---- OLD (source) databases ----
SRC_AUTH      = dict(host=OLD_HOST, port=5432, dbname="auth_db",      user=OLD_USER, password=OLD_PASSWORD)
SRC_ECOMMERCE = dict(host=OLD_HOST, port=5435, dbname="ecommerce_db", user=OLD_USER, password=OLD_PASSWORD)
SRC_SOCIAL    = dict(host=OLD_HOST, port=5436, dbname="social_db",    user=OLD_USER, password=OLD_PASSWORD)

# ---- NEW (target) databases: docker-compose exposes postgres on host 5440 ----
NEW_USER = "innovator"
NEW_PASSWORD = "innovator123"
NEW_HOST = "127.0.0.1"
NEW_PORT = 5440

def _new(db):
    return dict(host=NEW_HOST, port=NEW_PORT, dbname=db, user=NEW_USER, password=NEW_PASSWORD)

DST_AUTH      = _new("innovator_auth")
DST_ECOMMERCE = _new("innovator_ecommerce")
DST_FEED      = _new("innovator_feed")
DST_EVENTS    = _new("innovator_events")


def connect(cfg):
    return psycopg2.connect(**cfg)


def rows(conn, sql, params=None):
    with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cur:
        cur.execute(sql, params or ())
        return cur.fetchall()


def existing_ids(conn, table, id_col='"Id"'):
    with conn.cursor() as cur:
        cur.execute(f'SELECT {id_col} FROM {table};')
        return {r[0] for r in cur.fetchall()}


def dry_run_flag():
    return "--dry-run" in sys.argv
