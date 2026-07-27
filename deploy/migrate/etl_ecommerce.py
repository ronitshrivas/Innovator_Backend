#!/usr/bin/env python3
"""
Ecommerce:  ecommerce_db.*  ->  innovator_ecommerce.*

Migrates categories, products, product images, orders + items, payment QRs,
FCM tokens and notifications. UUID ids and timestamps are preserved so foreign
keys keep matching. Idempotent (skips rows whose id already exists).

Run:  python3 etl_ecommerce.py --dry-run    then    python3 etl_ecommerce.py
"""

import uuid
import json
from datetime import datetime, timezone
import psycopg2.extras
from mig_common import connect, rows, existing_ids, dry_run_flag, SRC_ECOMMERCE, DST_ECOMMERCE

DRY = dry_run_flag()
NOW = datetime.now(timezone.utc)


def run(dst, table, select_rows, columns, build, id_is_uuid=True):
    """Generic insert helper. `build` maps a source row -> tuple matching `columns`."""
    have = existing_ids(dst, f'public."{table}"') if id_is_uuid else set()
    placeholders = ",".join(["%s"] * len(columns))
    collist = ",".join(f'"{c}"' for c in columns)
    sql = f'INSERT INTO public."{table}" ({collist}) VALUES ({placeholders});'

    inserted = skipped = failed = 0
    for r in select_rows:
        rid = r.get("id")
        if id_is_uuid and rid in have:
            skipped += 1
            continue

        if DRY:
            inserted += 1
            continue

        # Row-by-row with a savepoint so one bad/duplicate row (e.g. a repeated
        # slug in the old data) is skipped instead of aborting the whole table.
        with dst.cursor() as cur:
            try:
                cur.execute("SAVEPOINT sp;")
                cur.execute(sql, build(r))
                cur.execute("RELEASE SAVEPOINT sp;")
                inserted += 1
            except Exception as ex:
                cur.execute("ROLLBACK TO SAVEPOINT sp;")
                failed += 1
                if failed <= 5:
                    print(f"    ! skipped one {table} row: {str(ex).splitlines()[0]}")

    tail = f" | failed: {failed}" if failed else ""
    print(f"  {table:22} {'would insert' if DRY else 'inserted'}: {inserted} | skipped: {skipped}{tail}")


def main():
    src = connect(SRC_ECOMMERCE)
    dst = connect(DST_ECOMMERCE)

    # Product name/image lookup for order items
    prod = {r["id"]: r for r in rows(src, "SELECT id, name, image FROM ecommerce_product;")}

    print("Ecommerce migration:")

    # Make category slugs unique (old data has repeated slugs, e.g. "electronics").
    _seen_slugs: set[str] = set()
    def _unique_slug(r):
        base = (r["slug"] or str(r["id"])[:8]).strip().lower()
        slug = base
        if slug in _seen_slugs:
            slug = f"{base}-{str(r['id'])[:8]}"
        _seen_slugs.add(slug)
        return slug

    run(dst, "ProductCategories",
        rows(src, "SELECT id, name, slug, description, created_at FROM ecommerce_category;"),
        ["Id", "Name", "Slug", "Description", "CreatedAt", "UpdatedAt"],
        lambda r: (str(r["id"]), r["name"], _unique_slug(r),
                   r["description"], r["created_at"], r["created_at"]))

    run(dst, "Products",
        rows(src, """SELECT id, name, description, price, stock, is_active,
                            category_id, image, created_at, updated_at
                     FROM ecommerce_product;"""),
        ["Id", "Name", "Description", "Price", "Stock", "IsActive",
         "CategoryId", "Image", "CreatedAt", "UpdatedAt"],
        lambda r: (str(r["id"]), r["name"], r["description"], r["price"] or 0,
                   r["stock"] or 0, bool(r["is_active"]),
                   str(r["category_id"]) if r["category_id"] else None,
                   r["image"], r["created_at"], r["updated_at"] or r["created_at"]))

    # Old product image id is bigint -> generate a new uuid.
    run(dst, "ProductImages",
        rows(src, "SELECT image, product_id, created_at FROM ecommerce_productimage;"),
        ["Id", "ProductId", "Image", "CreatedAt", "UpdatedAt"],
        lambda r: (str(uuid.uuid4()), str(r["product_id"]), r["image"],
                   r["created_at"], r["created_at"]),
        id_is_uuid=False)

    run(dst, "PaymentQrs",
        rows(src, "SELECT id, qr_image, name, user_id, created_at FROM ecommerce_paymentqr;"),
        ["Id", "VendorId", "VendorName", "Name", "Image", "IsActive", "CreatedAt", "UpdatedAt"],
        lambda r: (str(r["id"]), str(r["user_id"]) if r["user_id"] else "", "",
                   r["name"] or "", r["qr_image"] or "", True,
                   r["created_at"], r["created_at"]))

    run(dst, "Orders",
        rows(src, """SELECT id, customer_id, full_name, address, phone_number, notes,
                            payment_type, status, total_amount, shipping_charge,
                            payment_screenshot, created_at, updated_at
                     FROM ecommerce_order;"""),
        ["Id", "UserId", "FullName", "Address", "PhoneNumber", "Notes", "PaymentType",
         "Status", "TotalAmount", "ShippingCharge", "GrandTotal",
         "PaymentScreenshotPath", "KhaltiPidx", "CreatedAt", "UpdatedAt"],
        lambda r: (str(r["id"]), str(r["customer_id"]) if r["customer_id"] else None,
                   r["full_name"] or "", r["address"] or "", r["phone_number"] or "",
                   r["notes"], r["payment_type"] or "cod", r["status"] or "pending",
                   r["total_amount"] or 0, r["shipping_charge"] or 0,
                   (r["total_amount"] or 0) + (r["shipping_charge"] or 0),
                   r["payment_screenshot"], None, r["created_at"], r["updated_at"] or r["created_at"]))

    run(dst, "OrderItems",
        rows(src, "SELECT id, order_id, product_id, quantity, price FROM ecommerce_orderitem;"),
        ["Id", "OrderId", "ProductId", "ProductName", "Price", "Quantity", "LineTotal",
         "Image", "CreatedAt", "UpdatedAt"],
        lambda r: (str(r["id"]), str(r["order_id"]), str(r["product_id"]),
                   (prod.get(r["product_id"]) or {}).get("name", ""),
                   r["price"] or 0, r["quantity"] or 1,
                   (r["price"] or 0) * (r["quantity"] or 1),
                   (prod.get(r["product_id"]) or {}).get("image"),
                   NOW, NOW))

    run(dst, "FcmTokens",
        rows(src, "SELECT id, user_id, token, device_name, created_at, updated_at FROM ecommerce_fcmtoken;"),
        ["Id", "UserId", "Token", "Platform", "CreatedAt", "UpdatedAt"],
        lambda r: (str(r["id"]), str(r["user_id"]) if r["user_id"] else None,
                   r["token"] or "", (r["device_name"] or "android"),
                   r["created_at"], r["updated_at"] or r["created_at"]))

    run(dst, "Notifications",
        rows(src, """SELECT id, user_id, title, message, notification_type,
                            is_read, data, created_at
                     FROM ecommerce_notification;"""),
        ["Id", "UserId", "Title", "Message", "NotificationType", "IsRead",
         "DataJson", "CreatedAt", "UpdatedAt"],
        lambda r: (str(r["id"]), str(r["user_id"]) if r["user_id"] else None,
                   r["title"] or "", r["message"] or "", r["notification_type"] or "order",
                   bool(r["is_read"]),
                   json.dumps(r["data"]) if r["data"] is not None else "{}",
                   r["created_at"], r["created_at"]))

    if not DRY:
        dst.commit()
    src.close(); dst.close()
    print("Done." if not DRY else "Dry run complete (nothing written).")


if __name__ == "__main__":
    main()
