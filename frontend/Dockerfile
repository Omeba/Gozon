FROM nginx:alpine

COPY index.html /usr/share/nginx/html/

COPY nginx.conf /etc/nginx/nginx.conf

RUN mkdir -p /var/log/nginx

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]