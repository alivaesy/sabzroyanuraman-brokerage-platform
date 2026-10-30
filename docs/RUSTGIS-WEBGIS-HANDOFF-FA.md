# گزارش انتقال فنی: اتصال RustGIS به WebGIS سبز رویان

> این سند برای ادامه کار توسط حساب‌ها و گفت‌وگوهای دیگر نوشته شده است. پیش از هر تغییر جدید، وضعیت واقعی شاخه و GitHub Actions را دوباره بررسی کنید؛ وضعیت CI در این سند تاریخی است و نباید نتیجه قدیمی را نتیجه فعلی فرض کرد.

## 1. مخزن و محدوده کار

- مخزن: [alivaesy/sabzroyanuraman-brokerage-platform](https://github.com/alivaesy/sabzroyanuraman-brokerage-platform)
- رابط WebGIS: `sabzroyanuraman-site/web-gis.html`
- شاخه تغییرات: `feat/webgis-rustgis-integrated-package`
- Pull Request: [#41 — اتصال پردازش Vector با RustGIS و WASM](https://github.com/alivaesy/sabzroyanuraman-brokerage-platform/pull/41)
- شاخه مقصد PR: `master`

هدف معماری این است که رابط فعلی مبتنی بر Leaflet حفظ شود و پردازش‌های مکانی واقعی به‌وسیله کتابخانه‌های RustGIS در WebAssembly اجرا شوند. هدف، انتقال کامل برنامه مستقل RustGIS یا جایگزینی رابط Leaflet با رابط egui نیست.

## 2. RustGIS چگونه استفاده شده است؟

یک Adapter اختصاصی با Rust در مسیر زیر ساخته شده است:

`sabzroyanuraman-site/rustgis-wasm/`

این Adapter مستقیماً از دو crate پروژه RustGIS استفاده می‌کند:

- `rustgis-analysis`: اجرای الگوریتم‌ها و تحلیل‌های مکانی
- `rustgis-data`: خواندن و نوشتن داده‌های مکانی

وابستگی‌ها از مخزن upstream پروژه RustGIS دریافت شده و به commit زیر pin شده‌اند تا buildها قابل تکرار باشند:

`38f24336140d59092a31f964e9ac86aab18fdecd`

بنابراین عبارت دقیق برای توصیف پیاده‌سازی فعلی «استفاده از کتابخانه‌های تحلیل و داده RustGIS از طریق Adapter اختصاصی Rust/WASM» است. در این مرحله کل RustGIS Engine، Session یا رابط مستقل egui منتقل نشده است.

## 3. اجزای پیاده‌سازی‌شده

### 3.1. Adapter زبان Rust

فایل: `sabzroyanuraman-site/rustgis-wasm/src/lib.rs`

APIهای صادرشده برای JavaScript:

- `adapter_protocol_version()`
- `upstream_revision()`
- `adapter_status()`
- `list_tools()`
- `run_tool(tool_id, layers_json, params_json)`

جریان کلی اجرای Vector:

1. دریافت ورودی GeoJSON از JavaScript.
2. تبدیل GeoJSON به Dataset با `rustgis_data::io::read_geojson`.
3. دریافت ابزار و پارامترهای موردنیاز.
4. اجرای الگوریتم با `rustgis_analysis::run`.
5. تبدیل نتیجه به GeoJSON با `rustgis_data::io::write_bytes`.
6. بازگرداندن نتیجه به Runtime جاوااسکریپت.

برای ورودی Adapter محدودیت حجمی 128 MiB در نظر گرفته شده است. همچنین یک آزمون Native برای اجرای Buffer روی یک Point GeoJSON و بررسی خروجی Polygon نوشته شده است.

### 3.2. Runtime جاوااسکریپت

فایل: `sabzroyanuraman-site/webgis-rustgis-runtime.js`

وظایف:

- بارگذاری پویا و اجرای بسته WASM
- دریافت فهرست ابزارهای واقعی Adapter/RustGIS
- ایجاد پنل پردازش RG در کنار ArcToolbox
- ساخت فرم پارامترها بر اساس schema ابزارها
- انتخاب لایه‌های موجود در پروژه
- ارسال GeoJSON و پارامترها به WASM
- دریافت خروجی Vector و افزودن آن به پروژه

Runtime بسته WASM را از مسیر زیر انتظار دارد:

`./rustgis-wasm-pkg/sabzroyan_rustgis_wasm.js`

### 3.3. اتصال به WebGIS موجود

فایل: `sabzroyanuraman-site/web-gis.html`

یک Bridge اشتراکی با نام `window.__sabzWebGISSharedBridge` اضافه شده است. تابع `addGeoJSONLayer` امکان می‌دهد خروجی Vector به محیط موجود برگردد و به نقشه و ساختار لایه‌های WebGIS اضافه شود.

فایل Runtime نیز در صفحه بارگذاری شده است. در جریان اصلاح قبلی، محل درج script tag که ابتدا اشتباهاً داخل رشته HTML مربوط به popup قرار گرفته بود اصلاح شد؛ سپس inline scriptهای HTML از نظر Syntax بررسی شدند.

### 3.4. Workflow ساخت و آزمون

فایل:

`.github/workflows/webgis-rustgis-wasm.yml`

Workflow برای این مراحل تنظیم شده است:

- نصب Rust stable و هدف `wasm32`
- نصب `wasm-pack` و ابزارهای لازم
- اجرای آزمون‌های Native
- ساخت بسته WebAssembly
- بررسی Syntax فایل Runtime جاوااسکریپت
- بارگذاری Artifact در اجرای PR
- پس از push روی master، ساخت package و Cargo.lock و ثبت آن‌ها در مخزن برای میزبانی استاتیک

توجه: تا وقتی PR ادغام نشده باشد، Workflow مربوط به انتشار روی master فعال نمی‌شود.

### 3.5. مستندات و تست‌های همراه

- `sabzroyanuraman-site/rustgis-wasm/README.md`
- `sabzroyanuraman-site/RUSTGIS-INTEGRATION.md`
- `tests/web-gis-static.test.cjs`

در تست static، assertion مربوط به واردکردن فایل ArcMap اصلاح شد تا پسوند zip را نیز بپذیرد؛ زیرا کد موجود از قبل فرمت‌های `.mxd`، `.mpk`، `.mxt` و `.zip` را می‌پذیرفت.

## 4. وضعیت قابلیت‌ها و محدودیت‌ها

### موارد پیاده‌سازی‌شده در شاخه

- Adapter اختصاصی Rust متصل به crateهای `rustgis-analysis` و `rustgis-data`
- مسیر ورودی و خروجی Vector مبتنی بر GeoJSON
- اجرای ابزارهای Vector از طریق `rustgis_analysis::run`
- دریافت فایل اصلی GeoTIFF در مرورگر و نگهداری بایت‌های آن در رجیستری Raster همان نشست
- خواندن GeoTIFF در Rust با `rustgis_data::raster::geotiff::read`
- اجرای Slope، Aspect، Hillshade و Raster Calculator با توابع واقعی `rustgis_analysis::rasterops`
- نوشتن خروجی GeoTIFF با `rustgis_data::raster::geotiff::write` و حفظ CRS و GeoTransform شبکه ورودی
- دانلود فایل GeoTIFF و ساخت پیش‌نمایش Leaflet از خروجی؛ تصویر پیش‌نمایش جایگزین فایل اصلی نیست
- امکان استفاده از Raster در کنار لایه Vector برای ابزارهای سازگار مانند Zonal Statistics از طریق `Inputs::raster`
- آزمون Native برای Buffer و آزمون Raster Slope به‌همراه round-trip نوشتن و خواندن GeoTIFF

### محدودیت‌های باقی‌مانده

- بایت‌های Raster فعلاً در رجیستری نشست مرورگر نگهداری می‌شوند و هنوز در فایل پروژه WebGIS ذخیره نمی‌شوند.
- Selection Output هنوز به مدل انتخاب فعلی WebGIS اعمال نمی‌شود.
- همه ابزارهای Raster upstream، ابزارهای نیازمند مسیر فایل، GeoLibre و Pluginها متصل نشده‌اند.
- پردازش داده‌های بسیار بزرگ و streaming/windowed transfer هنوز پیاده‌سازی نشده است.
- آزمون End-to-End واقعی در مرورگر و تأیید روی هاست Production انجام نشده است.

PNG پیش‌نمایش نقشه، خروجی تحلیلی Raster نیست. خروجی تحلیلی همان GeoTIFF تولیدشده توسط RustGIS است.

## 5. وضعیت CI در آخرین گزارش قبلی

در آخرین بررسی ثبت‌شده در گفت‌وگوی قبلی:

- اجرای static WebGIS با Run ID `41025835472` موفق گزارش شده بود.
- اجرای Rust/WASM با Run ID `41025835642` و Job ID `123963610523` هنوز در حال اجرا بود.
- آزمون Native در حال اجرا بود و مرحله WASM build هنوز شروع نشده بود.
- فایل‌های log در آن زمان به علت خطای BlobNotFound قابل بازیابی نبودند.
- Syntax فایل JavaScript و inline scriptهای HTML بررسی و موفق گزارش شده بود.
- آزمون واقعی مرورگر انجام نشده بود.

**این اطلاعات صرفاً وضعیت تاریخی است.** ادامه‌دهنده باید آخرین commit شاخه و آخرین GitHub Actions مرتبط با HEAD فعلی PR #41 را بررسی کند و نتیجه Native Test و WASM Build را مستقلاً تأیید کند. نتیجه موفقیت build را از روی گزارش قبلی حدس نزند.

## 6. مرحله بعد از اتصال اولیه Raster

اتصال اولیه Raster و GeoTIFF در شاخه پیاده‌سازی شده است. اکنون ادامه کار باید روی تکمیل و اعتبارسنجی آن متمرکز باشد:

1. بررسی آخرین GitHub Actions برای HEAD فعلی PR و اطمینان از موفقیت Native Test، WASM Build و JavaScript syntax check.
2. آزمون با GeoTIFF واقعی چندباندی و DEM، از جمله فایل UTM و فایل WGS84.
3. بررسی صحت CRS، GeoTransform، Extent، اندازه پیکسل، NoData و مقادیر سلولی خروجی.
4. آزمون زنجیره‌ای: تولید خروجی Raster، انتخاب همان خروجی به‌عنوان ورودی پردازش بعدی، و اطمینان از باقی ماندن فایل اصلی در رجیستری.
5. بررسی و پیاده‌سازی ذخیره‌سازی Raster source و metadata در فایل پروژه WebGIS، به‌جای نگهداری صرفاً session-scoped.
6. افزودن آزمون E2E در مرورگر برای Import GeoTIFF، اجرای ابزار RustGIS، نمایش preview و دانلود GeoTIFF.
7. بررسی محدودیت حافظه WebAssembly و ارائه پیام خطای روشن برای Rasterهای بزرگ‌تر از سقف نمونه‌ها.

عملیات Raster متصل‌شده در مرحله فعلی: Slope، Aspect، Hillshade و Raster Calculator. وجود هر ابزار دیگر در فهرست هدف به معنی پشتیبانی آن در commit pin‌شده RustGIS نیست؛ ابتدا API upstream را بررسی کنید.

## 7. دستورالعمل ادامه کار

1. ابتدا PR شماره 41، شاخه و آخرین commit را بررسی کنید.
2. آخرین GitHub Actions مرتبط با HEAD شاخه را بررسی و نتیجه تست Native و WASM Build را ثبت کنید.
3. اگر build شکست خورده، log را بخوانید و ابتدا همان خطا را اصلاح کنید.
4. قبل از شروع Raster، ساختار واقعی داده Raster در WebGIS و APIهای Raster در commit قفل‌شده RustGIS را بررسی کنید.
5. تغییرات را در شاخه کاری مناسب و با حفظ سازگاری Vector انجام دهید.
6. تست‌های Native، WASM، static و در صورت امکان browser E2E را اجرا کنید.
7. گزارش نهایی را بر اساس شواهد CI و تست واقعی بنویسید؛ موارد انجام‌نشده را صریح مشخص کنید.

**محدودیت مجوز:** PR شماره 41 را بدون اجازه صریح مالک پروژه Merge نکنید. ادغام در master و انتشار روی سایت Live دو اقدام جداگانه‌اند و هیچ‌کدام را بدون تأیید مالک انجام ندهید.

## 8. هدف نهایی

حفظ رابط کاربری فعلی WebGIS و استفاده از RustGIS به‌عنوان موتور پردازش مکانی از طریق Rust/WASM؛ ابتدا برای Vector و سپس برای Raster و GeoTIFF. تمام قابلیت‌ها باید به الگوریتم‌های واقعی و خروجی‌های معتبر GIS متصل باشند، نه صرفاً عناصر نمایشی رابط کاربری.
