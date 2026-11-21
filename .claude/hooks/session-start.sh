#!/bin/bash
# Prepares a Claude Code cloud session: the owner's agent skills, pinned to reviewed commits, the .NET SDK global.json
# asks for, the repository's tools and packages, and the docs site's packages. A step that fails is named in the last
# line and does not stop the steps after it.
set -uo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# A step also ends where a $(...) fails; the bash of a local macOS session does not know the option
shopt -s inherit_errexit

cd "$CLAUDE_PROJECT_DIR"

# Bump a ref only after reading what changed upstream
PONYTAIL_REF=2961b52fc1fe6a5a87c7a959a79512e5c2f3ffe5
AGENT_SKILLS_REF=a851c53733a492a6c43b452a9bb6df8f39b87c53
HUMANIZER_REF=8c307f367c91bd974002f10f2be2965d642c13df

skills="$HOME/.claude/skills"
cache="$HOME/.cache/agent-skills"
failed=""

# Runs a step in a shell of its own, which the step's first failing command ends
step() {
  local name=$1
  shift

  (
    set -e
    "$@"
  )
  [ $? -eq 0 ] || failed="$failed $name"
}

checkout() {
  local repo=$1 ref=$2 dest="$cache/$(basename "$1")"
  if [ "$(git -C "$dest" rev-parse HEAD 2>/dev/null)" != "$ref" ]; then
    rm -rf "$dest"
    git init -q "$dest"
    git -C "$dest" fetch -q --depth 1 "https://github.com/$repo" "$ref"
    git -C "$dest" checkout -q FETCH_HEAD
  fi
  echo "$dest"
}

copy() {
  local source=$1
  rm -rf "$skills/$(basename "$source")"
  cp -r "$source" "$skills/"
}

install_skills() {
  mkdir -p "$skills" "$cache"

  ponytail=$(checkout DietrichGebert/ponytail "$PONYTAIL_REF")
  for skill in ponytail ponytail-review ponytail-audit ponytail-debt; do
    copy "$ponytail/skills/$skill"
  done

  owner=$(checkout Maxi10022/agent-skills "$AGENT_SKILLS_REF")
  for skill in dotnet-testing/skills/csharp-test-formatting dotnet-testing/skills/test-data-catalog \
    git-workflow/skills/git-commit git-workflow/skills/git-worktree; do
    copy "$owner/plugins/$skill"
  done

  # The docs skills run it over changed prose, with writing.md's Voice section as the voice sample
  humanizer=$(checkout blader/humanizer "$HUMANIZER_REF")
  rm -rf "$skills/humanizer"
  mkdir -p "$skills/humanizer"
  cp "$humanizer/SKILL.md" "$skills/humanizer/"
}

# Inside the repository `dotnet --version` fails until global.json is met, so the test follows the pin. The SDK goes
# next to the ones of the dotnet on the path, which then finds it, or to a root of its own when there is none.
install_sdk() {
  dotnet --version >/dev/null 2>&1 && return

  local root=/usr/share/dotnet
  if command -v dotnet >/dev/null; then
    root=$(dirname "$(readlink -f "$(command -v dotnet)")")
  fi

  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --jsonfile global.json --install-dir "$root"
  command -v dotnet >/dev/null || ln -sf "$root/dotnet" /usr/local/bin/dotnet
  dotnet --version >/dev/null
}

# The Native AOT publish of the console sample links with clang against zlib
install_aot_tools() {
  dpkg -s clang zlib1g-dev >/dev/null 2>&1 && return

  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq clang zlib1g-dev >/dev/null
}

# npm ci installs what the lock file says and never rewrites it. It runs again only when a hashed file changed.
install_docs_packages() {
  local hashes=node_modules/.session-start.sha256
  sha256sum --check --status "$hashes" 2>/dev/null && return

  rm -f "$hashes"
  npm ci --no-audit --no-fund
  sha256sum package.json package-lock.json > "$hashes"
}

step skills install_skills
step sdk install_sdk

if dotnet --version >/dev/null 2>&1; then
  step tools dotnet tool restore
  step packages dotnet restore
else
  sdk=$(sed -n 's/.*"version": *"\(.*\)".*/\1/p' global.json)
  echo "session-start: no .NET SDK $sdk, which global.json asks for; a limited network has to allow dot.net and" \
    "builds.dotnet.microsoft.com" >&2
fi

step aot install_aot_tools
step docs install_docs_packages

if [ -n "$failed" ]; then
  echo "session-start: failed:$failed" >&2
  exit 1
fi
