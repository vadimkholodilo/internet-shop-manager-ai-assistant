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

# Russian language: start from a copy of every en-gb language directory (so no key is ever missing), then
# overlay the community Russian pack (see language/README.md), committed to the repo so the build does not
# depend on GitHub. The checksum guards against the file being changed by accident. Only its translation
# files are used; its extension code (an admin updater) is not.
ARG RU_PACK_SHA256=c9ac8a5b40294e3a239e0421b3a81064454a6a43e76b0149694d313f8767a1e8
COPY language/ocn_language_russian.ocmod.zip /tmp/ru.zip
RUN find /var/www/html -type d -name en-gb -path '*language*' | while read -r d; do cp -a "$d" "$(dirname "$d")/ru-ru"; done \
    && echo "${RU_PACK_SHA256}  /tmp/ru.zip" | sha256sum -c - \
    && unzip -q /tmp/ru.zip -d /tmp/ru \
    && for app in catalog admin; do \
         cp -a "/tmp/ru/$app/language/ru-ru/." "/var/www/html/$app/language/ru-ru/" \
         && rm -rf "/var/www/html/$app/language/ru-ru/extension/opencart" \
         && cp -a "/tmp/ru/$app/language/ru-ru/extension/opencart/." "/var/www/html/extension/opencart/$app/language/ru-ru/"; \
       done \
    && rm -rf /tmp/ru /tmp/ru.zip \
    && sed -i -e "s/'Brand:'/'Бренд:'/" -e "s/'Product Code:'/'Артикул:'/" /var/www/html/catalog/language/ru-ru/product/product.php

COPY apache/opencart.conf /etc/apache2/conf-enabled/opencart.conf
COPY php/opencart.ini /usr/local/etc/php/conf.d/opencart.ini
# The catalog API is an OpenCart extension (extension/ai_assistant); provisioning registers it like Extensions > Modules > Install.
COPY extension/ai_assistant /var/www/html/extension/ai_assistant
COPY entrypoint.sh /usr/local/bin/shop-entrypoint
COPY provision.sh /usr/local/bin/shop-provision
RUN chmod +x /usr/local/bin/shop-entrypoint /usr/local/bin/shop-provision \
    && mkdir -p /state \
    && chown -R www-data:www-data /var/www/html /state

ENTRYPOINT ["shop-entrypoint"]
CMD ["apache2-foreground"]
