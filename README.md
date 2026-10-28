# 2026 C# Capstone - Sushi Toshi

Continuing project for **Sushi Toshi** to provide a comprehensive digital restaurant management platform.

<details>
<summary>Table of Contents</summary>

1. [Tech Stack](#-tech-stack)
2. [Project Structure](#-project-structure)
3. [Getting Started](#-getting-started)
4. [Usage](#usage)
5. [Roles & Permissions](#roles--permissions)
6. [Contributors](#contributors)

</details>

## 🔧 Tech Stack

- **Backend**: .NET 9 API
- **Frontend**: Next.js 16 using App Router, Material UI, CSS
- **Database**: MariaDB
- **Authentication**: OAuth, JWT
- **Deployment**: Docker

## 📁 Project Structure

```bash
├── docs # Project documentation (index: docs/README.md)
├── src #Project code
│ ├──back-end # .NET Core API application
│ ├──front-end # Next.js application
│ ├──maria-db # Database configuration
```

## 🚀 Getting Started

After installing the prerequisites, run the project with the following steps.

### Prerequisites

- Install Docker from [https://www.docker.com/](https://www.docker.com/)

- Install Node.js from [https://nodejs.org/en/download](https://nodejs.org/en/download)

- Install .NET 9 SDK from [https://dotnet.microsoft.com/en-us/download/dotnet/9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Install the Entity Framework CLI:

```bash
dotnet tool install --global dotnet-ef --version 9.*
```

### Steps

#### Database

- Initialize the database using Docker:

```bash
docker compose up -d
```

#### Backend

- Apply the latest Entity Framework Core migrations to the backend:

```bash
dotnet ef database update
```

- Run the backend:

```bash
dotnet run
```

#### Frontend

- Install dependencies:

```bash
npm install
```

- Build the frontend:

```bash
npm run build
```

- Run the frontend:

```bash
npm run start
```

- Open the project at [ http://localhost:3000](http://localhost:3000) or click the link in the terminal

- Open the api documentation at [ http://localhost:5264/swagger/](http://localhost:5264/swagger/)

## 📋 Usage

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
