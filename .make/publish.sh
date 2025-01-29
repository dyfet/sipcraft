#!/bin/sh
repo="$1"
verify=`git remote -v | grep push | head -1 | grep ${1}`
if test -z "$verify" ; then
	echo "*** not in release repo" ; exit 1 ; fi

status=`git status | grep "^Your branch is up to date"`
if test -z "$status" ; then
    echo "*** not in release state"
    exit 2
fi

current=`git branch -a | grep "^[*] " | cut -f2 -d\   `
case "$current" in
main|master)
    ;;
*)
    echo "*** not in release branch"
    exit 3
    ;;
esac
