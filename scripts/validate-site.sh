#!/bin/sh
set -eu

echo "Validating Sabz Royan Uraman website..."
test -f sabzroyanuraman-site/index.html
test -f sabzroyanuraman-site/determination-form.html
test -f sabzroyanuraman-site/brokerage-details.html
test -f sabzroyanuraman-site/web-gis.html
test -f sabzroyanuraman-site/web-cad.html
test -f sabzroyanuraman-site/.htaccess
test -f sabzroyanuraman-site/آپ.png
test -f sabzroyanuraman-site/سداد.png
grep -Fq 'src="/آپ.png"' sabzroyanuraman-site/brokerage-details.html
grep -Fq 'src="/سداد.png"' sabzroyanuraman-site/brokerage-details.html
grep -Fq 'مبلغ نهایی بر اساس تعرفه مصوب و در سامانه پرداخت محاسبه می‌شود.' sabzroyanuraman-site/brokerage-details.html
if grep -Fq '۵٬۰۰۰ تومان' sabzroyanuraman-site/brokerage-details.html; then
    echo "hardcoded-payment-amount-display"
    exit 1
fi
if grep -Fq 'src="/payment/آپ.png"' sabzroyanuraman-site/brokerage-details.html; then
    echo "broken-logo-path"
    exit 1
fi
if grep -Fq 'src="/payment/سداد.png"' sabzroyanuraman-site/brokerage-details.html; then
    echo "broken-logo-path"
    exit 1
fi
echo "Required website files and payment copy are valid."
