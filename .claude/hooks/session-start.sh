#!/bin/bash
# Prepares a Claude Code cloud session: the .NET SDK, the repository's tools and packages, the docs site's packages,
# and the owner's agent skills, pinned to reviewed commits.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

cd "$CLAUDE_PROJECT_DIR"

# dot.net and builds.dotnet.microsoft.com are not reachable from cloud sessions; Ubuntu's feed carries the SDK
if ! command -v dotnet >/dev/null 2>&1; then
  apt-get update -qq
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0 clang zlib1g-dev >/dev/null
fi

dotnet tool restore
dotnet restore || echo "dotnet restore failed; the solution may be mid-change" >&2
npm install --no-audit --no-fund

# Bump a ref only after reading what changed upstream
PONYTAIL_REF=2961b52fc1fe6a5a87c7a959a79512e5c2f3ffe5
AGENT_SKILLS_REF=a851c53733a492a6c43b452a9bb6df8f39b87c53
HUMANIZER_REF=8c307f367c91bd974002f10f2be2965d642c13df

skills="$HOME/.claude/skills"
cache="$HOME/.cache/agent-skills"
mkdir -p "$skills" "$cache"

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

install() {
  local source=$1
  rm -rf "$skills/$(basename "$source")"
  cp -r "$source" "$skills/"
}

ponytail=$(checkout DietrichGebert/ponytail "$PONYTAIL_REF")
for skill in ponytail ponytail-review ponytail-audit ponytail-debt; do
  install "$ponytail/skills/$skill"
done

owner=$(checkout Maxi10022/agent-skills "$AGENT_SKILLS_REF")
for skill in dotnet-testing/skills/csharp-test-formatting dotnet-testing/skills/test-data-catalog \
  git-workflow/skills/git-commit git-workflow/skills/git-worktree; do
  install "$owner/plugins/$skill"
done

# The docs skills run it over changed prose, with writing.md's Voice section as the voice sample
humanizer=$(checkout blader/humanizer "$HUMANIZER_REF")
rm -rf "$skills/humanizer"
mkdir -p "$skills/humanizer"
cp "$humanizer/SKILL.md" "$skills/humanizer/"
