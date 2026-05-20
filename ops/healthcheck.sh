#!/bin/sh
set -e
curl -fsS http://localhost:8080/health > /dev/null
