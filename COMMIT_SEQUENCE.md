# Suggested Commit Sequence

After extracting these files into the repository root:

```bash
git add README.md docs/README.md docs/00-product-vision.md
git commit -m "docs: establish CSweet product vision"

git add docs/01-domain-model.md
git commit -m "docs: define core company domain model"

git add docs/02-agent-orchestration.md
git commit -m "docs: define agent orchestration model"

git add docs/03-workforce-marketplace.md
git commit -m "docs: define unified workforce marketplace"

git add docs/04-remote-worker-provider-protocol.md
git commit -m "docs: define remote workforce provider protocol"

git add docs/05-human-workforce.md
git commit -m "docs: define human workforce and engagement model"

git add docs/06-budgeting-and-governance.md
git commit -m "docs: define budgeting authority and governance"

git add docs/07-security-privacy-and-trust.md
git commit -m "docs: define security privacy and trust boundaries"

git add docs/08-application-architecture.md
git commit -m "docs: define .NET and Blazor application architecture"

git add docs/09-prototype-roadmap.md docs/12-example-scenarios.md
git commit -m "docs: add prototype roadmap and validation scenarios"

git add docs/10-open-questions.md
git commit -m "docs: add open questions and decision log"

git add docs/11-brand-and-naming.md COMMIT_SEQUENCE.md
git commit -m "docs: capture naming notes and commit sequence"
```

Push the commits:

```bash
git push origin main
```

For a pull-request workflow:

```bash
git checkout -b planning/initial-architecture
# Run the commits above
git push -u origin planning/initial-architecture
```
