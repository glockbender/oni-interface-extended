PWSH ?= pwsh
PYTHON ?= python

.PHONY: check build package install translations

check translations:
	$(PWSH) -NoProfile -File ./tasks.ps1 -Task $@ -Python "$(PYTHON)"

build package install:
	$(PWSH) -NoProfile -File ./tasks.ps1 -Task $@ -Python "$(PYTHON)"
