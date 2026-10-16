PWSH ?= pwsh
PYTHON ?= python

.PHONY: check build package install translations test-agent test-runner workshop

check build package install translations test-agent test-runner workshop:
	$(PWSH) -NoProfile -File ./tasks.ps1 -Task $@ -Python "$(PYTHON)"
