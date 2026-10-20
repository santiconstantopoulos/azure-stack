# ---------- Etapa 1: build de Angular con Node ----------
FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

# ---------- Etapa 2: servir estáticos con nginx ----------
FROM nginx:alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf

# Angular genera la salida en dist/<nombre-del-proyecto>/browser (el proyecto se llama "web")
COPY --from=build /app/dist/web/browser /usr/share/nginx/html

EXPOSE 80
