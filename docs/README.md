# Sushi Toshi Documentation

Start with the section for your role.

## Customers and staff — using the app

None yet. Customer and staff guides will go in `docs/user/`.

## Restaurant admins — managing the restaurant

- [Admin Manual](admin/admin-manual.md): sessions, tables, locations, menus, staff, tags and analytics. A styled [HTML version](admin/admin-manual.html) is also available.

## System admins — deploying and configuring

- [Production setup](setup/production-setup.md): every setting to change before deploying, plus a pre-launch checklist
- [Email (SendGrid) setup](setup/email-sendgrid-setup.md): verification and password reset emails
- [WiFi configuration](setup/wifi-configuration.md): per-location WiFi credentials for QR codes

## Developers — working on the code

- [Getting started](../README.md#-getting-started): prerequisites and running the project locally
- [Google Sign-In setup](dev/google-signin-setup.md)
- [Testing QR codes on your phone](dev/phone-testing-setup.md): ngrok and user secrets
- [Testing](dev/testing.md): test suites, running tests and CI
- [Frontend testing](dev/frontend-testing.md): React component test templates and patterns
- [Team norms](dev/team-norms.md)
- [Database ERD](dev/architecture/erd.drawio) (open with draw.io)

### Docs that live next to the code

- [QR code service](../src/back-end/Services/QrCode/README.md): QR generation endpoints, folder layout and configuration
- [QR code management page](../src/front-end/src/components/admin/QR_CODE_INTEGRATION.md): the admin/staff QR code component, its API calls and UI

## Conventions

- File and folder names are lowercase kebab-case with no spaces, e.g. `staff-guide.md`.
- Images go in an `images/` folder beside the doc that uses them.
- Docs describe how things work now. Change history belongs in git.
- If you add a README next to the code, link it under "Docs that live next to the code" above.
