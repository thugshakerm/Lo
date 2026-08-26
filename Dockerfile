# MadXka API proxy (proxy/proxy.py) — repo-root context.
# BuildKit context is the repo root, so the proxy files are copied from proxy/.
FROM python:3.12-slim

WORKDIR /app

COPY proxy/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY proxy/ .

# Render injects the real PORT at runtime
ENV PORT=10000
EXPOSE 10000

CMD ["python", "proxy.py"]
