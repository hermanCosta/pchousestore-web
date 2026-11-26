# PcHouseStore local development bootstrap

This command provisions the first Company, Shop, Shop access credential and ADMIN employee in a fresh local development database.

It is intentionally restricted to a local PostgreSQL database named `pchousestore_dev`. It refuses to run if any Company, Shop or Employee already exists. It does not reset or overwrite existing credentials.

## Requirements

- PostgreSQL is running locally.
- All EF Core migrations have already been applied.
- The connection string points to the local `pchousestore_dev` database.

## Configure the environment (zsh)

Run from the repository root. These values exist only in the current terminal session. Do not put passwords in source files or commit them.

```bash
export ConnectionStrings__PcHouseStore="Host=localhost;Port=5432;Database=pchousestore_dev;Username=YOUR_POSTGRES_USER"

export EmployeeBootstrap__Operation="BootstrapDevelopment"
export DevelopmentBootstrap__Allow=true
export DevelopmentBootstrap__CompanyName="PcHouseStore"
export DevelopmentBootstrap__CompanyAddress="Company address"
export DevelopmentBootstrap__CompanyContact="Company phone"
export DevelopmentBootstrap__CountryCode="IE"
export DevelopmentBootstrap__CurrencyCode="EUR"
export DevelopmentBootstrap__TimeZone="Europe/Dublin"

export ShopCredentialProvisioning__Code="MAIN"
export ShopCredentialProvisioning__Name="Main Shop"
export ShopCredentialProvisioning__Address="Shop address"
export ShopCredentialProvisioning__Phone="Shop phone"

export EmployeeBootstrap__EmployeeCode="ADMIN"
export EmployeeBootstrap__FirstName="System"
export EmployeeBootstrap__LastName="Administrator"
export EmployeeBootstrap__Phone="Employee phone"
export EmployeeBootstrap__Email=""

printf "Shop password: "
read -s SHOP_PASSWORD
printf "\\nEmployee initial password: "
read -s ADMIN_PASSWORD
printf "\\n"
export ShopCredentialProvisioning__InitialPassword="$SHOP_PASSWORD"
export EmployeeBootstrap__InitialPassword="$ADMIN_PASSWORD"
unset SHOP_PASSWORD ADMIN_PASSWORD
```

The Shop password must contain 12–128 characters. The initial employee password must contain 6–128 characters.

## Run

```bash
dotnet run --project tools/PcHouseStore.Bootstrap --no-launch-profile
```

The command creates the Company, Shop, Shop credential and an ADMIN employee in one database transaction. Passwords are hashed using ASP.NET Core's `PasswordHasher<T>`; plaintext passwords are never written to the database or output.

The ADMIN employee is created with `MustChangePasswordAtNextUse = true`. Sign in to the Shop with its Shop code and password, then change the employee's initial password through the employee credential-change flow before performing protected actions.

After a successful run, remove the bootstrap values from the shell session:

```bash
unset EmployeeBootstrap__Operation
unset DevelopmentBootstrap__Allow DevelopmentBootstrap__CompanyName DevelopmentBootstrap__CompanyAddress DevelopmentBootstrap__CompanyContact DevelopmentBootstrap__CompanyCountryCode DevelopmentBootstrap__CurrencyCode DevelopmentBootstrap__TimeZone
unset ShopCredentialProvisioning__Code ShopCredentialProvisioning__Name ShopCredentialProvisioning__Address ShopCredentialProvisioning__Phone ShopCredentialProvisioning__InitialPassword
unset EmployeeBootstrap__EmployeeCode EmployeeBootstrap__FirstName EmployeeBootstrap__LastName EmployeeBootstrap__Phone EmployeeBootstrap__Email EmployeeBootstrap__InitialPassword
unset ConnectionStrings__PcHouseStore
```
