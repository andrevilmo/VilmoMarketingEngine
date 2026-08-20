FROM nginx:1.27-alpine
COPY gateway/nginx.conf /etc/nginx/nginx.conf
COPY site /usr/share/nginx/site
EXPOSE 80
HEALTHCHECK --interval=10s --timeout=3s --retries=6 \
  CMD wget -qO- http://127.0.0.1/health || exit 1
