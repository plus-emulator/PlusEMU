-- The Wired Archive page text as on Habbo (hhus NORMAL catalogue read r94, 2026-10-15, AIR WIN63-202610131318-860371703, CatalogPage 2735
-- of page wired_archive). Migration 82 created the page with empty texts. Idempotent.

UPDATE catalog_page_texts t JOIN catalog_pages c ON c.id = t.page_id
SET t.text = 'Archived Wired boxes that are still available for legacy reasons, but are not recommended to use in new Wired setups'
WHERE c.link = 'wired_archive' AND t.slot = 0;
