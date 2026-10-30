const { chromium } = require("playwright");

(async () => {
  const browser = await chromium.launch({ headless: true, args: ["--no-sandbox"] });
  try {
    const page = await browser.newPage();
    page.on("pageerror", error => process.stderr.write("PAGE ERROR: " + error.message + "\n"));
    await page.goto("http://127.0.0.1:4173/raster-runtime-tests.html", { waitUntil: "networkidle", timeout: 90000 });
    await page.locator("#run").click();
    await page.waitForFunction(() => {
      const text = document.querySelector("#out")?.textContent || "";
      return text.includes("ALL TESTS PASSED") || text.includes("FAIL:");
    }, { timeout: 180000 });
    const output = await page.locator("#out").textContent();
    process.stdout.write(output + "\n");
    if (!output.includes("ALL TESTS PASSED")) process.exitCode = 1;
  } finally {
    await browser.close();
  }
})().catch(error => {
  process.stderr.write((error.stack || error.message || String(error)) + "\n");
  process.exitCode = 1;
});
