# Copyright (C) 2022 Tycho Softworks.
#
# This file is free software; as a special exception the author gives
# unlimited permission to copy and/or distribute it, with or without
# modifications, as long as this notice is preserved.
#
# This program is distributed in the hope that it will be useful, but
# WITHOUT ANY WARRANTY, to the extent permitted by law; without even the
# implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.

.PHONY: vendor dist distclean mindist

ifndef ARCHIVE
ARCHIVE := $(PROJECT)
endif

dist:	vendor
	@rm -f $(ARCHIVE)-*.tar.gz $(ARCHIVE)-*.tar
	@git archive -o $(ARCHIVE)-$(VERSION).tar --format tar --prefix=$(ARCHIVE)-$(VERSION)/ v$(VERSION) 2>/dev/null || git archive -o $(ARCHIVE)-$(VERSION).tar --format tar --prefix=$(ARCHIVE)-$(VERSION)/ HEAD
	@if test -d vendor ; then \
		tar --transform s:^:$(ARCHIVE)-$(VERSION)/: --append --file=$(ARCHIVE)-$(VERSION).tar vendor ; fi
	@if test -f .make/nuget.config ; then \
		tar --transform s:^.make/:$(ARCHIVE)-$(VERSION)/: --append --file=$(ARCHIVE)-$(VERSION).tar .make/nuget.config ; fi
	@gzip $(ARCHIVE)-$(VERSION).tar

mindist:
	@rm -f $(ARCHIVE)-*.tar.gz $(ARCHIVE)-*.tar
	@git archive -o $(ARCHIVE)-mindist.tar --format tar --prefix=$(ARCHIVE)-mindist/ HEAD
	@gzip $(ARCHIVE)-mindist.tar

vendor:	restore
	@.make/vendor.sh $(DOTNET)
