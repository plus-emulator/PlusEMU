#!/usr/bin/env bash
# Keep the release private until every asset has uploaded. Reruns resume drafts.
set -euo pipefail

tag=$1
title=$2
commit=$3
shift 3

if draft=$(gh release view "$tag" --json isDraft --jq .isDraft); then
    if [ "$draft" = false ]; then
        echo "$tag is already published"
        exit 0
    fi
else
    gh release create "$tag" --target "$commit" --title "$title" --generate-notes --draft
fi

gh release upload "$tag" "$@" --clobber
gh release edit "$tag" --draft=false --latest
