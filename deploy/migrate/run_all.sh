#!/usr/bin/env bash
# Runs every migration in dependency order. Users MUST go first (everything
# references user ids). E-learning and reels are intentionally NOT migrated.
#
# Usage:
#   ./run_all.sh --dry-run     # report only, writes nothing
#   ./run_all.sh               # perform the migration
set -euo pipefail
cd "$(dirname "$0")"

FLAG="${1:-}"

echo "==== 1/4 users ===="       && python3 etl_users.py     $FLAG
echo "==== 2/4 ecommerce ===="   && python3 etl_ecommerce.py $FLAG
echo "==== 3/4 feed (no reels) ====" && python3 etl_feed.py  $FLAG
echo "==== 4/4 events ===="      && python3 etl_events.py    $FLAG

echo "All migrations finished."
