# 2026 C# Capstone - Sushi Toshi

Continuing project for **Sushi Toshi** to provide a comprehensive digital restaurant management platform.

<details>
<summary>Table of Contents</summary>

1. [Tech Stack](#-tech-stack)
2. [Project Structure](#-project-structure)
3. [Getting Started](#-getting-started)
4. [Running Tests](#-running-tests)
5. [Usage](#-usage)
6. [Roles & Permissions](#-roles--permissions)
7. [Contributors](#-2026-contributors)

</details>

## 🔧 Tech Stack

- **Backend**: .NET 9 API
- **Frontend**: Next.js 16 using App Router, Material UI, CSS
- **Database**: MariaDB
- **Authentication**: JWT, with email/password and Google Sign-In
- **Email**: Twilio SendGrid
- **Deployment**: Docker

## 📁 Project Structure

```bash
├── docs                          # Project documentation (index: docs/README.md)
├── src                           # Project code
│   ├── back-end                  # .NET API application
│   ├── back-end.Tests            # Backend unit tests
│   ├── back-end.IntegrationTests # Backend integration tests
│   ├── front-end                 # Next.js application
│   ├── maria-db                  # Database configuration
│   └── src.sln                   # .NET solution file
├── docker-compose.yml            # MariaDB container
└── run-all-tests.ps1             # Runs all automated tests
```

## 🚀 Getting Started

After installing the prerequisites, run the project with the following steps.

### Prerequisites

- Install Docker from [https://www.docker.com/](https://www.docker.com/)
- Install Node.js 20.9 or later from [https://nodejs.org/en/download](https://nodejs.org/en/download)
- Install .NET 9 SDK from [https://dotnet.microsoft.com/en-us/download/dotnet/9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Install the Entity Framework CLI:

```bash
dotnet tool install --global dotnet-ef --version 9.*
```

### Steps

#### Database

From the repo root, start the database using Docker:

```bash
docker compose up -d
```

#### Backend

From `src/back-end`:

- Apply the latest Entity Framework Core migrations:

```bash
dotnet ef database update
```

- Run the backend:

```bash
dotnet run --launch-profile http
```

#### Frontend

From `src/front-end`:

- Install dependencies:

```bash
npm install
```

- Run the frontend:

```bash
npm run dev
```

- Open the project at [http://localhost:3000](http://localhost:3000) or click the link in the terminal
- Open the API documentation at [http://localhost:5264/swagger/](http://localhost:5264/swagger/)

### Optional Setup

The app runs without these, but some features stay off until they're configured:

- **Google Sign-In**: see [docs/dev/google-signin-setup.md](docs/dev/google-signin-setup.md)
- **Verification and password reset emails**: see [docs/setup/email-sendgrid-setup.md](docs/setup/email-sendgrid-setup.md)
- **Testing QR codes on your phone**: see [docs/dev/phone-testing-setup.md](docs/dev/phone-testing-setup.md)

## 🧪 Running Tests

From the repo root, run all automated tests with PowerShell:

```bash
./run-all-tests.ps1
```

See [docs/dev/testing.md](docs/dev/testing.md) for running individual suites and for how CI works.

## 📋 Usage

Guides for admins, setup and development are listed in [docs/README.md](docs/README.md).

## 👥 Roles & Permissions

| Role            | Permissions                                                                         |
| --------------- | ----------------------------------------------------------------------------------- |
| Customers       | Browse menu, submit orders, request assistance, and manage billing.                 |
| Employees/Staff | Manage tables, sessions and customer requests.                                      |
| Admins          | Full access to table, sessions, location, staff, and menu management and analytics. |

## 👨‍💻 2026 Contributors

- Allison Wilson/hesherkid
- Arlyssa McArthur/Arlyssa
- Mehrnoush Bagheri/merbagheri

## 🔱 Forked from

Original project by [CDAJT_C-Capstone](https://github.com/SiberianIntergrations/CDAJT_C-Capstone)

### Original Contributors

- Timothy Torpy/TimothyTorpy
- Evan Gamble/TheHolographicP
- Josh Mantei/jmantei
- David Rochefort/bitroch
- Alex M/Pewpy
- Clarisse Buniel/seclaris
