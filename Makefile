PWSH ?= pwsh
PYTHON ?= python

.PHONY: check build package install translations test-agent test-runner

check translations test-runner:
	$(PWSH) -NoProfile -File ./tasks.ps1 -Task $@ -Python "$(PYTHON)"

build package install test-agent:
	$(PWSH) -NoProfile -File ./tasks.ps1 -Task $@ -Python "$(PYTHON)"
