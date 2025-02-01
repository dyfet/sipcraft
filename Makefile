# Copyright (C) 2025 Tycho Softworks. Licensed under CC BY-NC-ND 4.0.

# Project constants
PROJECT := sipcraft
VERSION := 0.0.1
DOTNET	:= net8.0
ORIGIN	:= github.com:tychosoft/
PATH	:= ./bin/Debug/$(DOTNET):${PATH}

ifndef  DESTDIR
DESTDIR =
endif

PREFIX = /usr
SYSCONFDIR = /etc
BINDIR = /usr/bin
SBINDIR = /usr/sbin
LOCALSTATEDKR = /var/lib/$(PROJECT)
LOGPREFIXDIR = /var/log

PATH := ./bin/Debug/$(DOTNET):${PATH}

.PHONY: debug release publish docker install clean verify

all:            debug           # default target debug
verify:		publish		# verify on full local build

debug:
	@dotnet build --no-restore -f $(DOTNET) -c Debug

release:
	@dotnet build -f $(DOTNET) -c Release

publish:
	@dotnet publish $(PROJECT).csproj -f $(DOTNET) -o bin/Install/$(DOTNET) -c Release /p:defineConstants="\"INSTALL;RELEASE;UNIX\""

docker:
	@dotnet publish $(PROJECT).csproj -f $(DOTNET) -o bin/Docker/$(DOTNET) -c Release --self-contained
	@docker build -t sipcraft .

install:        publish
	@install -d -m 755 $(DESTDIR)$(SBINDIR)
	@install -d -m 755 $(DESTDIR)$(SYSCONFDIR)
	@install $(PROJECT).conf $(DESTDIR)$(SYSCONFDIR)/
	@install bin/Install/$(DOTNET)/$(PROJECT) $(DESTDIR)$(SBINDIR)/

# Optional make components we may add
sinclude .make/*.mk

