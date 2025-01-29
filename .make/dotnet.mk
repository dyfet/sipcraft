# Copyright (C) 2024 Tycho Softworks.
#
# This file is free software; as a special exception the author gives
# unlimited permission to copy and/or distribute it, with or without
# modifications, as long as this notice is preserved.
#
# This program is distributed in the hope that it will be useful, but
# WITHOUT ANY WARRANTY, to the extent permitted by law; without even the
# implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.

.PHONY:	clean upgrade restore deps licenses version tag

clean:
	@dotnet clean

upgrade:
	@dotnet outdated --upgrade
	@dotnet restore

restore:
	@dotnet restore

deps:
	@dotnet list package

licenses:
	@dotnet-project-licenses --input $(ARCHIVE).csproj --include-transitive

version:
	@echo $(VERSION)

tag:	publish
	@.make/publish $(ORIGIN)
	@git tag v$(VERSION)
	@echo "release $(VERSION) packaged and tagged"

