#!/bin/sh
if [ "$GIT_AUTHOR_EMAIL" = "your.email@example.com" ]; then
	export GIT_AUTHOR_NAME="Sapir Gerstman"
	export GIT_AUTHOR_EMAIL="Sapir.Gerstman@e.braude.ac.il"
fi
if [ "$GIT_COMMITTER_EMAIL" = "your.email@example.com" ]; then
	export GIT_COMMITTER_NAME="Sapir Gerstman"
	export GIT_COMMITTER_EMAIL="Sapir.Gerstman@e.braude.ac.il"
fi
