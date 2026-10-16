#!/bin/sh
# The workspace's tests: each line is one expectation.
set -e
cd "$(dirname "$0")"
[ "$(sh greet.sh)" = "Hello, world!" ] || { echo "FAIL: greet.sh without a name"; exit 1; }
[ "$(sh greet.sh Ada)" = "Hello, Ada!" ] || { echo "FAIL: greet.sh with a name"; exit 1; }
echo "2 tests passed"
