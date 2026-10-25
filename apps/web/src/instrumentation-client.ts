import { startBrowserErrorTracking } from "./error-tracking/browser";

// Runs after the HTML is loaded and before React starts: the browser's error tracking, with the DSN from the page (F55).
startBrowserErrorTracking(document.documentElement.dataset);
