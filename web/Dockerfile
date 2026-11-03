# Sürümler bilerek sabitlenmiştir (yeniden üretilebilir derleme). Güncellemek için yeni sürüm etiketini
# https://hub.docker.com adresinden doğrulayıp burada ve docker-compose.yml'de birlikte değiştirin.

# 1. aşama: Vite ile derle
FROM node:22.21-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# 2. aşama: nginx ile sun
FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY security-headers.conf /etc/nginx/security-headers.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
