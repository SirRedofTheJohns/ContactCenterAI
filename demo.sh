#!/bin/sh
# Self-contained resort demo with Docker (macOS/Linux). Usage: ./demo.sh start|stop|status|logs|reset
set -eu
cd "$(dirname "$0")"
compose="docker compose -f deploy/demo/compose.yaml"
command -v docker >/dev/null 2>&1 || { echo "Docker is required."; exit 1; }
case "${1:-start}" in
  start)
    docker info >/dev/null 2>&1 || { echo "Docker is not running. Start it and try again."; exit 1; }
    echo "Building and starting the demo (the first run downloads images and packages)..."
    $compose up -d --build --wait || { echo "The demo did not become ready. Diagnose with: $compose logs"; exit 1; }
    echo "Demo ready: http://127.0.0.1:7452"
    echo "Fictional users: customer-a, customer-b, operations-admin - password 123456Aa!" ;;
  stop) $compose stop ;;
  status) $compose ps ;;
  logs) $compose logs --tail 200 ;;
  reset)
    printf 'This permanently deletes the demo data of project "contactcenterai-demo". Type DELETE to continue: '
    read -r answer
    [ "$answer" = "DELETE" ] || { echo "Nothing was deleted."; exit 0; }
    $compose down -v ;;
  *) echo "Usage: ./demo.sh start|stop|status|logs|reset"; exit 2 ;;
esac
