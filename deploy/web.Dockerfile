FROM nginx:1.27-alpine
COPY src/Vilmo.Web/wwwroot /usr/share/nginx/html
COPY template-metronic/themeforest-p1yvR6ry-metronic-responsive-admin-dashboard-template/metronic-v9.5.0/metronic-tailwind-html-starter-kit/dist/assets/css /usr/share/nginx/html/assets/css
COPY template-metronic/themeforest-p1yvR6ry-metronic-responsive-admin-dashboard-template/metronic-v9.5.0/metronic-tailwind-html-starter-kit/dist/assets/js/core.bundle.js /usr/share/nginx/html/assets/js/core.bundle.js
COPY template-metronic/themeforest-p1yvR6ry-metronic-responsive-admin-dashboard-template/metronic-v9.5.0/metronic-tailwind-html-starter-kit/dist/assets/vendors/keenicons /usr/share/nginx/html/assets/vendors/keenicons
COPY template-metronic/themeforest-p1yvR6ry-metronic-responsive-admin-dashboard-template/metronic-v9.5.0/metronic-tailwind-html-starter-kit/dist/assets/vendors/ktui /usr/share/nginx/html/assets/vendors/ktui
COPY template-metronic/themeforest-p1yvR6ry-metronic-responsive-admin-dashboard-template/metronic-v9.5.0/metronic-tailwind-html-starter-kit/dist/assets/media/app /usr/share/nginx/html/assets/media/app
COPY deploy/web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
HEALTHCHECK --interval=10s --timeout=3s --retries=6 CMD wget -qO- http://127.0.0.1/health || exit 1
