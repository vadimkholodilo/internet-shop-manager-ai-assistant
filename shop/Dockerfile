FROM php:8.2-apache

ARG OPENCART_VERSION=4.1.0.4

RUN apt-get update \
    && apt-get install -y --no-install-recommends unzip curl mariadb-client \
       libfreetype6-dev libjpeg62-turbo-dev libpng-dev libzip-dev libwebp-dev \
    && docker-php-ext-configure gd --with-freetype --with-jpeg --with-webp \
    && docker-php-ext-install -j"$(nproc)" gd zip mysqli \
    && a2enmod rewrite headers \
    && rm -rf /var/lib/apt/lists/*

# OpenCart release, pinned for reproducible demos.
RUN curl -fsSL -o /tmp/opencart.zip \
        "https://github.com/opencart/opencart/releases/download/${OPENCART_VERSION}/opencart-${OPENCART_VERSION}.zip" \
    && unzip -q /tmp/opencart.zip -d /tmp/opencart \
    && cp -a /tmp/opencart/upload/. /var/www/html/ \
    && rm -rf /tmp/opencart /tmp/opencart.zip /var/www/html/index.html

# Russian language: start from a copy of every en-gb language directory (so no key is ever missing),
# then overlay the translated files from shop/language/.
RUN find /var/www/html -type d -name en-gb -path '*language*' | while read -r d; do cp -a "$d" "$(dirname "$d")/ru-ru"; done
COPY language /tmp/language
RUN php /tmp/language/apply.php /var/www/html /tmp/language/ru-translations.php && rm -rf /tmp/language

COPY apache/opencart.conf /etc/apache2/conf-enabled/opencart.conf
COPY php/opencart.ini /usr/local/etc/php/conf.d/opencart.ini
COPY catalog_api/controller /var/www/html/catalog/controller/catalog_api
COPY entrypoint.sh /usr/local/bin/shop-entrypoint
COPY provision.sh /usr/local/bin/shop-provision
RUN chmod +x /usr/local/bin/shop-entrypoint /usr/local/bin/shop-provision \
    && mkdir -p /state \
    && chown -R www-data:www-data /var/www/html /state

ENTRYPOINT ["shop-entrypoint"]
CMD ["apache2-foreground"]
