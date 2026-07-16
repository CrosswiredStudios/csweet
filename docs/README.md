# CSweet Planning Documents

This directory captures the current product, architecture, marketplace, security, and delivery plan.

## Recommended reading order

1. `00-product-vision.md`
2. `01-domain-model.md`
3. `02-agent-orchestration.md`
4. `03-workforce-marketplace.md`
5. `04-remote-worker-provider-protocol.md`
6. `05-human-workforce.md`
7. `06-budgeting-and-governance.md`
8. `07-security-privacy-and-trust.md`
9. `08-application-architecture.md`
10. `09-prototype-roadmap.md`
11. `10-open-questions.md`
12. `11-brand-and-naming.md`
13. `12-example-scenarios.md`
14. `13-system-boundaries-and-deployment.md`
15. `14-application-design-system.md`

## Maintenance guidance

- Update `10-open-questions.md` when assumptions become decisions.
- Create ADRs for decisions with significant architectural consequences.
- Keep marketplace-provider contracts separate from internal domain models.
- Keep Microsoft Agent Framework behind application-owned abstractions.
- Add scenario tests for every major workflow introduced.
- Keep `14-application-design-system.md` and the shared `--cs-*` CSS tokens synchronized for every visual-system change.
- Treat these documents as living plans rather than immutable specifications.
