# MadXka API proxy — proxy/ directory as build context.
# Use this one if the Render service's "Root Directory" is set to proxy/.
FROM python:3.12-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Render injects the real PORT at runtime
ENV PORT=10000
EXPOSE 10000

CMD ["python", "proxy.py"]
