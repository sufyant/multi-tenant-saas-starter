/**
 * The most entries the Activity log's CSV file holds: the newest 100,000, about 10.5 MB, which the browser keeps whole as the
 * file it saves (`apiDownload`), from 1,000 pages of the API. The maintainer's decision of 2026-11-07, after the measure of
 * 37a-frontend-3. The file writes up to it, the dialog names it and the toast says when a file holds it.
 */
export const activityFileCap = 100_000;
