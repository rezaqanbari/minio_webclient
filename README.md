# سامانه تحت وب مدیریت سرور MinIO (ASP.NET Core 9)

این پروژه یک پنل مدیریت وب مدرن، زیبا و واکنش‌گرا (RTL) است که با استفاده از **ASP.NET Core 9 (MVC)** و کتابخانه رسمی **MinIO C# SDK** توسعه یافته است تا بتوانید سرور MinIO خود را به راحتی مدیریت کنید.

---

## مشخصات اتصال پیش‌فرض

تنظیمات در فایل `appsettings.json` ذخیره شده‌اند:

```json
{
  "Minio": {
    "Endpoint": "your-minio-server.com",
    "AccessKey": "YOUR_MINIO_ACCESS_KEY",
    "SecretKey": "YOUR_MINIO_SECRET_KEY",
    "WithSSL": true,
    "Region": ""
  }
}
```

---

## امکانات سامانه

1. **داشبورد وضعیت (Dashboard):**
   - نمایش وضعیت اتصال بلادرنگ (آنلاین/آفلاین) به سرور
   - نمایش آدرس سرور و پروتکل امن SSL (HTTPS)
   - نمایش تعداد کل باکت‌های فعال
   - امکان ایجاد سریع باکت جدید

2. **مدیریت باکت‌ها (Buckets):**
   - مشاهده لیست کامل باکت‌ها به همراه تاریخ ایجاد
   - جستجوی بلادرنگ در نام باکت‌ها
   - ایجاد باکت جدید مطابق با استانداردهای نام‌گذاری MinIO/AWS S3
   - حذف باکت همراه با تاییدیه امنیتی

3. **کاوشگر فایل و پوشه (File & Object Explorer):**
   - مرور محتویات هر باکت به صورت سلسله‌مراتبی و پوشه‌بندی‌شده
   - نوار ناوبری مسیر (Breadcrumbs)
   - تشخیص خودکار نوع فایل و نمایش آیکون اختصاصی (ویدیو، تصویر، صوت، PDF، متن، آرشیو و ...)
   - آپلود فایل‌های حجیم (تا سقف ۵۰۰ مگابایت) با قابلیت **Drag & Drop** چندگانه
   - ساخت پوشه مجازی جدید
   - دانلود مستقیم فایل‌ها با نام و پسوند اصلی
   - حذف تکی فایل یا حذف کامل پوشه همراه با کلیه زیرمجموعه‌های آن

4. **پیش‌نمایش آنلاین فایل‌ها (Media & Document Preview):**
   - پخش آنلاین فایل‌های ویدیویی (MP4, WebM, MOV) درون مرورگر
   - پخش آنلاین فایل‌های صوتی (MP3, WAV, OGG, AAC)
   - نمایش تصاویر با کیفیت اصلی (JPG, PNG, GIF, WebP, SVG)
   - مشاهده فایل‌های متنی، اسناد، لاگ‌ها و JSON درون ادیتور پیش‌نمایش
   - مشاهده اسناد PDF داخل پنجره Modal

5. **لینک‌های دانلود موقت و امن (Presigned URLs):**
   - صدور لینک مستقیم و معتبر با قابلیت تعیین مدت اعتبار (از ۱ ساعت تا ۷ روز)
   - امکان کپی مستقیم لینک در کلیپ‌بورد با یک کلیک یا باز کردن مستقیم در مرورگر

---

## نحوه راه‌اندازی و اجرا با Docker (ساده‌ترین روش روی سرور)

### روش اول: استفاده از Docker Compose (توصیه شده)

1. مخزن را روی سرور کلون کنید:
```bash
git clone https://github.com/rezaqanbari/minio_webclient.git
cd minio_webclient
```

2. فایل تنظیمات متغیرهای محیطی `.env` را از روی نمونه بسازید:
```bash
cp .env.example .env
```

3. فایل `.env` را با یک ویرایشگر باز کرده و آدرس و کلیدهای سرور MinIO خود را وارد کنید:
```env
PORT=5175
MINIO_ENDPOINT=your-minio-server.com
MINIO_ACCESS_KEY=YOUR_MINIO_ACCESS_KEY
MINIO_SECRET_KEY=YOUR_MINIO_SECRET_KEY
MINIO_WITH_SSL=true
MINIO_REGION=
```

4. با دستور زیر پروژه را بیلد و اجرا کنید:
```bash
docker compose up -d --build
```

اکنون سامانه روی پورت `5175` در دسترس است:
`http://SERVER_IP:5175`

---

### روش دوم: اجرای مستقیم با دستور Docker Run

```bash
docker build -t minio_webclient .

docker run -d \
  --name minio_webclient \
  --restart unless-stopped \
  -p 5175:8080 \
  -e MINIO_ENDPOINT="your-minio-server.com" \
  -e MINIO_ACCESS_KEY="YOUR_MINIO_ACCESS_KEY" \
  -e MINIO_SECRET_KEY="YOUR_MINIO_SECRET_KEY" \
  -e MINIO_WITH_SSL=true \
  minio_webclient
```

---

## نحوه اجرا به صورت لوکال بدون داکر

### با دستور dotnet CLI:
```bash
cd minio_webclient
dotnet run --launch-profile http
```
سپس مرورگر را باز کرده و به آدرس `http://localhost:5175` بروید.

### با Visual Studio / Rider:
فایل پروژه `minio_csharpClient.csproj` را باز کرده و دکمه **Run / Debug (F5)** را بزنید.
