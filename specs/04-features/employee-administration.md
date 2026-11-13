# Feature: Employee administration

**Status:** APPROVED — product rules and technical patterns approved 2026-11-12  
**Owner:** Product owner / technical lead  
**Depends on:** AUTH-01, DATA-02, approved `MANAGER` / `TECHNICIAN` role matrix

## 1. Objective

Allow an authorized Manager to administer Company-wide employee profiles, credentials, active status and role assignments. The Shop remains the persistent session identity. A Manager must provide their individual employee credential for every committed administration operation, and that one-use authorization is consumed atomically with the write.

Implementation follows the approved contracts below. Audit-retention duration remains a go-live decision.

## 2. Legacy evidence

| Type | Source and method | Confirmed behavior |
|---|---|---|
| Swing view | `src/main/java/com/pchouse/pchousestoremvn/views/EmployeeView.java` — `loadEmployeeListTable`, `getEmployeeFields`, `searchEmployee`, `getItemEmployee`, `btn_addActionPerformed`, `btn_updateActionPerformed`, `btn_deleteActionPerformed`, `table_view_employeesMouseClicked` | Lists employee ID, name, contact, email, username and free-form access level; creates/edits a selected employee; confirms before update/delete; checks password confirmation; prevents add when a matching contact number is found; searches employee records. The form requires first name, last name, contact number, username, password, confirmation and an access-level selection; email is optional. It uppercases names/username/access level and lowercases email. |
| Controller | `src/main/java/com/pchouse/pchousestoremvn/controllers/EmployeeController.java` — `getAllEmployee`, `addEmployee`, `updateEmployee`, `deleteEmployee`, `getItemEmployee`, `getEmployeeByPass`, `searchEmployeeByContactNo` | Directly delegates employee list, create, update, delete, lookup and contact-search operations to `EmployeeDAO`. |
| DAO | `src/main/java/com/pchouse/pchousestoremvn/dao/EmployeeDAO.java` — `getAllEmployeeDAO`, `addEmployeeDAO`, `updateEmployeeDAO`, `deleteEmployeeDAO`, `getItemEmployeeDAO`, `getEmployeeByPasswordDAO`, `searchEmployeeByContactNoDAO` | Lists all rows without Company scope; persists/deletes with JPA; the credential lookup searches by password alone. These behaviors are migration evidence, not approved target behavior. |
| Model / mapping | `src/main/java/com/pchouse/pchousestoremvn/models/Employee.java` — fields `person`, `username`, `password`, `accessLevel`; `src/main/java/com/pchouse/pchousestoremvn/models/Person.java` — `firstName`, `lastName`, `contactNo`, optional `email` | Employee references Person with `@ManyToOne`; access level is a `String`, not an enum. Person name/contact columns are required; email is optional. |
| Enum / persistence | Authentication-specific | No Employee role enum was found. `Employee.accessLevel` is a free-form string. The target uses the approved Company-wide `MANAGER` / `TECHNICIAN` role assignments instead. |

## 3. Approved target decisions

- Employee identity and credentials are Company-wide and work in every Shop belonging to that Company.
- Employee code is unique within the Company and is the identifier used with the password challenge. The legacy username field is not retained separately. The target stores only an adaptive password hash.
- Phone number and email are registration fields, not identity keys. They are not unique, and the system does not automatically link an Employee to an existing Person by contact details.
- Managers may provision, disable and reset employee credentials. A reset forces a password change at next use. An employee may change their own password after entering the current password.
- The Product Owner selected the initial temporary employee password for bootstrap only. Its literal must be supplied through local secret configuration and must never be committed or stored in plaintext; the bootstrap Manager must change it before any protected operation. Newly provisioned employees receive individual credentials through the Manager flow and must change them before any protected operation.
- In the Manager employee-create form, default the temporary password to the normalized employee code followed by `@pchouse`. The Manager may replace this value; the 12-character minimum remains enforced. Every newly created employee must change the initial password before any other protected operation.
- The last active Manager may be disabled or lose the final `MANAGER` role assignment. If this leaves the Company without a Manager, the identity-verified deployment-controlled owner/operator recovery process applies; no public recovery endpoint is allowed.
- Employee records cannot be hard-deleted through staff administration. A Manager deactivates an employee; historical employee references remain intact.
- Any authenticated Shop session may load/search employee registration records within its Company without an employee credential challenge. Individual employee profile reads and employee mutations remain Manager-authorized; employee identity is never cached as the persistent session.
- Employee account passwords have no time-based expiry. Bootstrap and newly provisioned/reset credentials require a change before any other protected operation; other password changes/resets do not require a reason.
- Employee administration changes are audited with actor EmployeeId, target EmployeeId, action and timestamp. No employee administration action requires a reason. Passwords and password hashes are never stored in audit events.
- Employee roles are Company-wide `MANAGER` and `TECHNICIAN`; an employee may hold both and receives the union of permissions. `MANAGER` grants full access.
- Employee password policy is minimum 12 characters, accepts at least 128 characters, has no periodic expiry, and failed attempts do not lock or disable accounts. Authentication endpoints enforce the approved source/network rate limit.
- Employee profile data is represented by Company-owned Person and Employee records. `Employee.PersonId` is nullable in the target mapping, consistent with the legacy optional `@ManyToOne` relationship.
- Employee registration requires first name, last name and phone number; email is optional. These registration details are not used as the logged-in/profile identity, which always displays the active Shop's name, address and phone.
- Every Manager operation requires an individual, fresh, one-use `EmployeeActionAuthorization`; ShopSession never stores the Manager identity.

See [Employee Access and Roles](../02-architecture/employee-access-and-roles.md), [Product Owner Decisions](../02-architecture/product-owner-decisions.md) and [Authentication and Shop Context](authentication-and-shop-context.md).

## 4. Proposed target scope (REDESIGN)

Manager screen, scoped to the Company resolved from the active ShopSession. The persistent profile/context header always shows the authenticated Shop's name, address and phone. Employee name/contact data is maintained for staff registration only; employees never sign in to establish a persistent profile. The acting employee is identified by employee code and password only when a protected operation is requested, such as committing a sale or service order.

The hamburger navigation opens one administration page at a time: Shop profile, Shop access credential, Change my employee password, Load employees, Create employee, or Employee directory. The Shop identity/session header and navigation remain available across pages.

- Search and list employees with profile name, employee code, contact, email, active state and assigned roles; never return or display password hashes or reset credentials.
- Create an Employee with a required Person profile (first name, last name and phone; email optional), assign one or both approved roles, and default its temporary password to normalized employee code plus `@pchouse` (editable; minimum 12 characters). Require that employee to change the password before any other protected operation.
- Edit profile fields and Company-wide roles.
- Disable/reactivate an employee and reset an employee credential. A reset requires a fresh Manager challenge and sets a mandatory next-use password-change flag.
- Employee self-service password change verifies the current password and applies the approved password baseline, without time-based expiry or a reason field.
- Use soft deactivation for the target rather than reproducing the legacy hard delete. Preserve Employee references in historical records.
- Candidate versioned routes: `POST /api/v1/employees/queries` (read/search), `POST /api/v1/employees` (create), `GET/PUT /api/v1/employees/{employeeId}` (read/update), `PATCH /api/v1/employees/{employeeId}/status`, `PUT /api/v1/employees/{employeeId}/roles`, `POST /api/v1/employees/{employeeId}/credential-reset`, and `PUT /api/v1/employees/me/credential`.
- Any active Shop session may search the Company employee directory. Manager authorization is required for individual employee profile reads, create/update, deactivation/reactivation, role changes and credential reset. The API validates a fresh employee code/password for Manager actions; self-password change verifies the current employee credentials. The UI does not persist Employee identity.
- Bind each action authorization to the Company for create actions or the target Employee for employee-specific actions. Do not include passwords in operation-context hashes. Directory reads require the Shop session and create an audit event attributed to that session; Manager-authorized reads create an employee-attributed audit event. Writes consume a single-use authorization in the same transaction as persistence.
- Preserve an immutable employee administration event with CompanyId, actor EmployeeId, target EmployeeId, action code and timestamp. No reason is required. Password hashes and plaintext passwords are excluded. Audit retention follows the Company data-retention policy (retention period remains a go-live decision).
- Bootstrap remains deployment-controlled with the configured initial secret, stored only as an adaptive hash; the bootstrap Manager must change it before any other protected command. Newly provisioned employees receive individual passwords from their Manager and must change them before any other protected command.

The bootstrap secret value is supplied through local secret configuration and is never committed. No public bootstrap or Manager recovery endpoint is provided.

## 5. API contracts

Every request below is bearer-authenticated as the active Shop. Individual employee profile reads and mutations are additionally authorized by a fresh employee action authorization. Directory query requires only the active Shop session and does not request employee credentials. All requests are Company-scoped from ShopSession; directory queries create audit events attributed to the ShopSession.

| Endpoint | Action code/resource | Request DTO |
|---|---|---|
| `POST /api/v1/employees/queries` | Shop session / Company | `{ "search": "optional text" }` |
| `POST /api/v1/employees` | `EMPLOYEE_CREATE` / Company | `{ "employeeCode", "firstName", "lastName", "phone", "email": null, "initialPassword", "roles": ["MANAGER"] }` |
| `GET /api/v1/employees/{employeeId}` | `EMPLOYEE_READ` / Employee | No body |
| `PUT /api/v1/employees/{employeeId}` | `EMPLOYEE_PROFILE_UPDATE` / Employee | `{ "firstName", "lastName", "phone", "email": null }` |
| `PATCH /api/v1/employees/{employeeId}/status` | `EMPLOYEE_STATUS_CHANGE` / Employee | `{ "isActive": false }` |
| `PUT /api/v1/employees/{employeeId}/roles` | `EMPLOYEE_ROLE_CHANGE` / Employee | `{ "roles": ["MANAGER", "TECHNICIAN"] }` |
| `POST /api/v1/employees/{employeeId}/credential-reset` | `EMPLOYEE_CREDENTIAL_RESET` / Employee | `{ "newPassword" }` |
| `PUT /api/v1/employees/me/credential` | `EMPLOYEE_SELF_PASSWORD_CHANGE` / Employee | `{ "newPassword" }` |

`EmployeeDto` contains employee ID/code, first/last name, phone, optional email, active state, roles, must-change-password state and credential version only where required for conflict handling. It never contains a password/hash. Employee code is unique within Company; contact/email may repeat. Employee records are deactivated, never hard-deleted. Invalid input returns RFC 9457 problem details; duplicate employee codes return `409 employee_code_conflict`, wrong resource/role returns `403`, replay returns `409 authorization_replayed`, and an employee who must change their password receives `403 employee_password_change_required` for all actions except self-password change.

Audit retention duration follows the Company data-retention policy and remains a go-live decision; it does not block local implementation.

## 6. Acceptance criteria (draft)

- All employee list/search/read/write operations are restricted to the Company in the active ShopSession.
- Any active Shop session may load/search the employee directory within its Company. Only an active employee with effective `MANAGER` permission may read an individual employee profile or administer employee records.
- Each committed Manager operation consumes one fresh authorization bound to the action and target Employee; cross-Company targets and replay are rejected.
- Employee credentials are hashed, never returned, logged or included in audit payloads.
- Reset forces password change according to the approved first-use flow; self-service change requires the current password.
- Newly provisioned employees must change their initial password before any other protected action; no password expires based on elapsed time.
- Deactivation and role changes take effect for subsequent employee challenges and cannot leave unauthorized reusable action tokens.
- Historical records preserve their employee actor references.
- Authentication failure never locks/disables the employee; source/network rate limiting remains enforced.

## 7. Implementation record

- Implemented Company-scoped employee directory/search and detail reads, employee creation/profile updates, role assignment, deactivate/reactivate, credential reset, and self-service password change API routes.
- Manager operations use fresh one-use employee challenges; authorization consumption, audit event, and protected mutation are committed atomically. Directory/detail reads also create audit events.
- Newly created/reset employees must change their credential before any protected operation. Employee identity is not stored in the Shop session.
- Added the English Manager administration UI with one hamburger destination shown at a time, and a default employee temporary password derived from the employee code. The persistent profile continues to display the active Shop identity.
- Added and applied `20261113004053_EmployeeAdministration` to the local `pchousestore_dev` database.
- Deployment-controlled Manager provisioning/recovery and explicit operator credential reset are implemented by the local `PcHouseStore.Bootstrap` command; it has no HTTP endpoint. Production use still depends on restricted deployment-host access and operator identity logs. Audit-retention duration remains a go-live decision.
- Build validation is recorded in the implementation handoff; automated tests and end-to-end PostgreSQL authorization validation remain pending.

### Deployment-controlled bootstrap procedure

- Implemented as `tools/PcHouseStore.Bootstrap`; migration `20261113010857_DeploymentOperatorEmployeeBootstrap` has been applied to the local database. `BootstrapManager` is the default operation; `ResetEmployeeCredential` is an explicit operator operation that increments credential version and requires the employee to change the password before protected use.
- Use a local operator command with no HTTP listener. Database access is limited to a deployment operator account; the deployment host's access controls and operator logs provide identity verification.
- Build/run with `dotnet run --project tools/PcHouseStore.Bootstrap/PcHouseStore.Bootstrap.csproj`. Inject `ConnectionStrings__PcHouseStore` and `EmployeeBootstrap__CompanyId`, `EmployeeCode`, `FirstName`, `LastName`, `Phone`, and `InitialPassword` through the deployment environment or secret store. Never pass the initial password as a CLI argument or put it in source control.
- Load Company ID, employee code, first/last name, phone and initial password from environment/secret-store configuration. Never accept the password as a command-line argument or print it. The initial password is hashed before persistence and sets `MustChangePasswordAtNextUse=true`.
- Lock the Company row and allow the command only when that Company exists and has no active employee with an active `MANAGER` role. The same procedure supports initial setup and recovery after all Managers are unavailable. Existing Manager credentials are never overwritten.
- In the same transaction, add the Person, active Employee, `MANAGER` assignment and an immutable administration event. The event identifies the deployment operator by the host OS account and machine; ordinary Manager events continue to identify the acting Employee.
- Abort on incomplete configuration, invalid password length, duplicate employee code, missing Company or any active Manager. Do not implement a public endpoint or silently retry with generated credentials.
