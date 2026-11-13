import { useState, type FormEvent } from "react";
import { apiBaseUrl } from "../auth/oidc";

type Employee = {
  employeeId: string;
  employeeCode: string;
  firstName: string | null;
  lastName: string | null;
  phone: string | null;
  email: string | null;
  isActive: boolean;
  roles: string[];
  mustChangePasswordAtNextUse: boolean;
};

type ShopContext = { companyId: string };
type ActionAuthorization = { authorizationToken: string };
type Props = ShopContext & { getShopAccessToken: () => Promise<string | null> };
type Profile = Pick<Employee, "firstName" | "lastName" | "phone" | "email">;

export default function EmployeeAdministration({ companyId, getShopAccessToken }: Props) {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [directoryLoaded, setDirectoryLoaded] = useState(false);
  const [search, setSearch] = useState("");
  const [employeeCode, setEmployeeCode] = useState("");
  const [managerPassword, setManagerPassword] = useState("");
  const [newEmployee, setNewEmployee] = useState({ employeeCode: "", firstName: "", lastName: "", phone: "", email: "", initialPassword: "", roles: ["TECHNICIAN"] });
  const [selfChange, setSelfChange] = useState({ employeeCode: "", currentPassword: "", newPassword: "" });
  const [busyAction, setBusyAction] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function managerRequest<T>(
    actionCode: string,
    resourceType: "Company" | "Employee",
    resourceId: string,
    path: string,
    method: string,
    body: unknown,
    credentials: { code: string; password: string },
  ): Promise<T | null> {
    const token = await getShopAccessToken();
    if (!token) throw new Error("Your Shop session has expired. Sign in again.");
    const challenge = await fetch(`${apiBaseUrl}/api/v1/employee-action-authorizations`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
      body: JSON.stringify({
        employeeCode: credentials.code,
        password: credentials.password,
        actionCode,
        resourceType,
        resourceId,
        targetShopId: null,
      }),
    });
    if (!challenge.ok) throw await responseError(challenge);
    const authorization = await challenge.json() as ActionAuthorization;
    const response = await fetch(`${apiBaseUrl}${path}`, {
      method,
      headers: {
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
        "X-Employee-Action-Authorization": authorization.authorizationToken,
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) throw await responseError(response);
    if (response.status === 204) return null;
    return await response.json() as T;
  }

  async function loadDirectory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await runAction("employee-directory", async () => {
      const response = await managerRequest<{ items: Employee[] }>(
        "EMPLOYEE_DIRECTORY_READ", "Company", companyId, "/api/v1/employees/queries", "POST", { search },
        { code: employeeCode, password: managerPassword },
      );
      setEmployees(response?.items ?? []);
      setDirectoryLoaded(true);
      setNotice("Employee directory loaded.");
    });
  }

  async function createEmployee(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await runAction("employee-create", async () => {
      const response = await managerRequest<Employee>(
        "EMPLOYEE_CREATE", "Company", companyId, "/api/v1/employees", "POST", newEmployee,
        { code: employeeCode, password: managerPassword },
      );
      if (response) setEmployees(current => [...current, response].sort((left, right) => left.employeeCode.localeCompare(right.employeeCode)));
      setNewEmployee({ employeeCode: "", firstName: "", lastName: "", phone: "", email: "", initialPassword: "", roles: ["TECHNICIAN"] });
      setNotice("Employee created. Share the initial password securely; the employee must change it before their first protected action.");
    });
    setNewEmployee(current => ({ ...current, initialPassword: "" }));
  }

  async function updateProfile(event: FormEvent<HTMLFormElement>, employee: Employee, profile: Profile) {
    event.preventDefault();
    await runAction(`profile-${employee.employeeId}`, async () => {
      await managerRequest<never>("EMPLOYEE_PROFILE_UPDATE", "Employee", employee.employeeId,
        `/api/v1/employees/${encodeURIComponent(employee.employeeId)}`, "PUT", profile,
        { code: employeeCode, password: managerPassword });
      setEmployees(current => current.map(item => item.employeeId === employee.employeeId ? { ...item, ...profile } : item));
      setNotice(`Updated ${employee.employeeCode}.`);
    });
  }

  async function updateStatus(employee: Employee) {
    await runAction(`status-${employee.employeeId}`, async () => {
      await managerRequest<never>("EMPLOYEE_STATUS_CHANGE", "Employee", employee.employeeId,
        `/api/v1/employees/${encodeURIComponent(employee.employeeId)}/status`, "PATCH", { isActive: !employee.isActive },
        { code: employeeCode, password: managerPassword });
      setEmployees(current => current.map(item => item.employeeId === employee.employeeId ? { ...item, isActive: !item.isActive } : item));
      setNotice(`${employee.employeeCode} ${employee.isActive ? "deactivated" : "reactivated"}.`);
    });
  }

  async function updateRoles(employee: Employee, roles: string[]) {
    await runAction(`roles-${employee.employeeId}`, async () => {
      await managerRequest<never>("EMPLOYEE_ROLE_CHANGE", "Employee", employee.employeeId,
        `/api/v1/employees/${encodeURIComponent(employee.employeeId)}/roles`, "PUT", { roles },
        { code: employeeCode, password: managerPassword });
      setEmployees(current => current.map(item => item.employeeId === employee.employeeId ? { ...item, roles } : item));
      setNotice(`Updated roles for ${employee.employeeCode}.`);
    });
  }

  async function resetCredential(event: FormEvent<HTMLFormElement>, employee: Employee, newPassword: string) {
    event.preventDefault();
    await runAction(`reset-${employee.employeeId}`, async () => {
      await managerRequest<never>("EMPLOYEE_CREDENTIAL_RESET", "Employee", employee.employeeId,
        `/api/v1/employees/${encodeURIComponent(employee.employeeId)}/credential-reset`, "POST", { newPassword },
        { code: employeeCode, password: managerPassword });
      setEmployees(current => current.map(item => item.employeeId === employee.employeeId
        ? { ...item, mustChangePasswordAtNextUse: true } : item));
      setNotice(`Reset the password for ${employee.employeeCode}. They must change it before their next protected action.`);
    });
  }

  async function changeOwnPassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await runAction("self-password-change", async () => {
      await managerRequest<never>("EMPLOYEE_SELF_PASSWORD_CHANGE", "Employee", "self",
        "/api/v1/employees/me/credential", "PUT", { newPassword: selfChange.newPassword },
        { code: selfChange.employeeCode, password: selfChange.currentPassword });
      setSelfChange({ employeeCode: "", currentPassword: "", newPassword: "" });
      setNotice("Your employee password has been changed.");
    }, false);
  }

  async function runAction(key: string, action: () => Promise<void>, requireManager = true) {
    setBusyAction(key);
    setError(null);
    setNotice(null);
    try {
      if (requireManager && (!employeeCode.trim() || !managerPassword))
        throw new Error("Enter the Manager employee code and password for this operation.");
      await action();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "The employee operation failed.");
    } finally {
      setManagerPassword("");
      setEmployeeCode("");
      setSelfChange(current => ({ ...current, currentPassword: "", newPassword: "" }));
      setBusyAction(null);
    }
  }

  function toggleNewRole(role: string) {
    setNewEmployee(current => ({
      ...current,
      roles: current.roles.includes(role) ? current.roles.filter(value => value !== role) : [...current.roles, role],
    }));
  }

  return (
    <section className="employee-admin" aria-labelledby="employee-admin-title">
      <div className="employee-admin-heading">
        <div>
          <h2 id="employee-admin-title">Employee administration</h2>
          <p className="form-help">Employee identity is verified for each operation and is never kept as a signed-in profile.</p>
        </div>
      </div>

      {error && <p className="auth-error" role="alert">{error}</p>}
      {notice && <p className="auth-notice" role="status">{notice}</p>}

      <form className="auth-form employee-action-form" onSubmit={loadDirectory}>
        <h3>Manager verification</h3>
        <p className="form-help">Enter your employee code and password whenever you load or change employee records.</p>
        <div className="employee-form-grid">
          <label className="auth-field">Manager employee code
            <input required maxLength={40} autoComplete="username" value={employeeCode} onChange={event => setEmployeeCode(event.target.value)} />
          </label>
          <label className="auth-field">Manager password
            <input required type="password" maxLength={128} autoComplete="current-password" value={managerPassword} onChange={event => setManagerPassword(event.target.value)} />
          </label>
          <label className="auth-field">Search employees
            <input maxLength={200} value={search} onChange={event => setSearch(event.target.value)} placeholder="Name, code, phone or email" />
          </label>
        </div>
        <button disabled={busyAction !== null}>{busyAction === "employee-directory" ? "Loading…" : "Load employees"}</button>
      </form>

      <form className="auth-form employee-create-form" onSubmit={createEmployee}>
        <h3>Create employee</h3>
        <div className="employee-form-grid">
          <label className="auth-field">Employee code
            <input required maxLength={40} value={newEmployee.employeeCode} onChange={event => setNewEmployee(current => ({ ...current, employeeCode: event.target.value }))} />
          </label>
          <label className="auth-field">First name
            <input required value={newEmployee.firstName} onChange={event => setNewEmployee(current => ({ ...current, firstName: event.target.value }))} />
          </label>
          <label className="auth-field">Last name
            <input required value={newEmployee.lastName} onChange={event => setNewEmployee(current => ({ ...current, lastName: event.target.value }))} />
          </label>
          <label className="auth-field">Phone
            <input required value={newEmployee.phone} onChange={event => setNewEmployee(current => ({ ...current, phone: event.target.value }))} />
          </label>
          <label className="auth-field">Email (optional)
            <input type="email" value={newEmployee.email} onChange={event => setNewEmployee(current => ({ ...current, email: event.target.value }))} />
          </label>
          <label className="auth-field">Initial password
            <input type="password" required minLength={12} maxLength={128} autoComplete="new-password" value={newEmployee.initialPassword} onChange={event => setNewEmployee(current => ({ ...current, initialPassword: event.target.value }))} />
          </label>
        </div>
        <p className="form-help">The employee must change this password before performing any protected action.</p>
        <RoleChecks roles={newEmployee.roles} onToggle={toggleNewRole} />
        <button disabled={busyAction !== null}>{busyAction === "employee-create" ? "Creating…" : "Create employee"}</button>
      </form>

      {directoryLoaded && (
        <div className="employee-directory">
          <h3>Employees ({employees.length})</h3>
          {employees.length === 0 && <p className="form-help">No employees match this search.</p>}
          <div className="employee-list">
            {employees.map(employee => <EmployeeCard key={employee.employeeId} employee={employee} busy={busyAction !== null}
              employeeCode={employeeCode} managerPassword={managerPassword} onEmployeeCodeChange={setEmployeeCode} onManagerPasswordChange={setManagerPassword}
              onUpdateProfile={updateProfile} onUpdateStatus={updateStatus} onUpdateRoles={updateRoles} onResetCredential={resetCredential} />)}
          </div>
        </div>
      )}

      <form className="auth-form employee-password-change" onSubmit={changeOwnPassword}>
        <h3>Change my employee password</h3>
        <p className="form-help">Use this after an initial password or reset. This changes your employee credential, not the Shop sign-in password.</p>
        <div className="employee-form-grid">
          <label className="auth-field">Employee code
            <input required maxLength={40} autoComplete="username" value={selfChange.employeeCode} onChange={event => setSelfChange(current => ({ ...current, employeeCode: event.target.value }))} />
          </label>
          <label className="auth-field">Current employee password
            <input required type="password" maxLength={128} autoComplete="current-password" value={selfChange.currentPassword} onChange={event => setSelfChange(current => ({ ...current, currentPassword: event.target.value }))} />
          </label>
          <label className="auth-field">New employee password
            <input required type="password" minLength={12} maxLength={128} autoComplete="new-password" value={selfChange.newPassword} onChange={event => setSelfChange(current => ({ ...current, newPassword: event.target.value }))} />
          </label>
        </div>
        <button disabled={busyAction !== null}>{busyAction === "self-password-change" ? "Changing…" : "Change employee password"}</button>
      </form>
    </section>
  );
}

function EmployeeCard({
  employee,
  busy,
  employeeCode,
  managerPassword,
  onEmployeeCodeChange,
  onManagerPasswordChange,
  onUpdateProfile,
  onUpdateStatus,
  onUpdateRoles,
  onResetCredential,
}: {
  employee: Employee;
  busy: boolean;
  employeeCode: string;
  managerPassword: string;
  onEmployeeCodeChange: (value: string) => void;
  onManagerPasswordChange: (value: string) => void;
  onUpdateProfile: (event: FormEvent<HTMLFormElement>, employee: Employee, profile: Profile) => Promise<void>;
  onUpdateStatus: (employee: Employee) => Promise<void>;
  onUpdateRoles: (employee: Employee, roles: string[]) => Promise<void>;
  onResetCredential: (event: FormEvent<HTMLFormElement>, employee: Employee, password: string) => Promise<void>;
}) {
  const [profile, setProfile] = useState<Profile>({ firstName: employee.firstName, lastName: employee.lastName, phone: employee.phone, email: employee.email });
  const [roles, setRoles] = useState(employee.roles);
  const [resetPassword, setResetPassword] = useState("");

  function toggleRole(role: string) {
    setRoles(current => current.includes(role) ? current.filter(value => value !== role) : [...current, role]);
  }

  return (
    <details className="employee-card">
      <summary>
        <span className="employee-card-identity"><strong>{employee.employeeCode}</strong><span>{[employee.firstName, employee.lastName].filter(Boolean).join(" ") || "Profile details not set"}</span></span>
        <span className={employee.isActive ? "employee-state active" : "employee-state"}>{employee.isActive ? "Active" : "Inactive"}</span>
        <span>{employee.roles.length > 0 ? employee.roles.join(" · ") : "No roles"}</span>
        {employee.mustChangePasswordAtNextUse && <span className="employee-password-state">Password change required</span>}
      </summary>
      <div className="employee-card-body">
        <form className="auth-form" onSubmit={event => void onUpdateProfile(event, employee, profile)}>
          <h4>Registration profile</h4>
          <div className="employee-form-grid">
            <label className="auth-field">First name<input required value={profile.firstName ?? ""} onChange={event => setProfile(current => ({ ...current, firstName: event.target.value }))} /></label>
            <label className="auth-field">Last name<input required value={profile.lastName ?? ""} onChange={event => setProfile(current => ({ ...current, lastName: event.target.value }))} /></label>
            <label className="auth-field">Phone<input required value={profile.phone ?? ""} onChange={event => setProfile(current => ({ ...current, phone: event.target.value }))} /></label>
            <label className="auth-field">Email (optional)<input type="email" value={profile.email ?? ""} onChange={event => setProfile(current => ({ ...current, email: event.target.value }))} /></label>
          </div>
          <CredentialFields busy={busy} code={employeeCode} password={managerPassword} onCodeChange={onEmployeeCodeChange} onPasswordChange={onManagerPasswordChange} />
          <button disabled={busy}>Save profile</button>
        </form>
        <form className="auth-form" onSubmit={event => { event.preventDefault(); void onUpdateRoles(employee, roles); }}>
          <h4>Company-wide roles</h4>
          <RoleChecks roles={roles} onToggle={toggleRole} />
          <CredentialFields busy={busy} code={employeeCode} password={managerPassword} onCodeChange={onEmployeeCodeChange} onPasswordChange={onManagerPasswordChange} />
          <button disabled={busy}>Save roles</button>
        </form>
        <form className="auth-form" onSubmit={event => void onResetCredential(event, employee, resetPassword).finally(() => setResetPassword(""))}>
          <h4>Reset employee password</h4>
          <label className="auth-field">New temporary password<input type="password" required minLength={12} maxLength={128} autoComplete="new-password" value={resetPassword} onChange={event => setResetPassword(event.target.value)} /></label>
          <CredentialFields busy={busy} code={employeeCode} password={managerPassword} onCodeChange={onEmployeeCodeChange} onPasswordChange={onManagerPasswordChange} />
          <button disabled={busy}>Reset password</button>
        </form>
        <div className="employee-status-action">
          <p>{employee.isActive ? "Deactivate this employee to prevent future employee challenges." : "Reactivate this employee to allow employee challenges again."}</p>
          <CredentialFields busy={busy} code={employeeCode} password={managerPassword} onCodeChange={onEmployeeCodeChange} onPasswordChange={onManagerPasswordChange} />
          <button type="button" className="secondary" disabled={busy} onClick={() => void onUpdateStatus(employee)}>{employee.isActive ? "Deactivate employee" : "Reactivate employee"}</button>
        </div>
      </div>
    </details>
  );
}

function RoleChecks({ roles, onToggle }: { roles: string[]; onToggle: (role: string) => void }) {
  return (
    <fieldset className="employee-roles">
      <legend>Roles</legend>
      {["MANAGER", "TECHNICIAN"].map(role => <label key={role}><input type="checkbox" checked={roles.includes(role)} onChange={() => onToggle(role)} />{role}</label>)}
    </fieldset>
  );
}

function CredentialFields({ busy, code, password, onCodeChange, onPasswordChange }: {
  busy: boolean;
  code: string;
  password: string;
  onCodeChange: (value: string) => void;
  onPasswordChange: (value: string) => void;
}) {
  return (
    <div className="employee-verification-fields">
      <label className="auth-field">Manager employee code<input required maxLength={40} autoComplete="username" value={code} onChange={event => onCodeChange(event.target.value)} disabled={busy} /></label>
      <label className="auth-field">Manager password<input required type="password" maxLength={128} autoComplete="current-password" value={password} onChange={event => onPasswordChange(event.target.value)} disabled={busy} /></label>
    </div>
  );
}

async function responseError(response: Response): Promise<Error> {
  try {
    const body = await response.json() as { detail?: string; code?: string };
    return new Error(body.detail ?? body.code ?? `Request failed (HTTP ${response.status}).`);
  } catch {
    return new Error(`Request failed (HTTP ${response.status}).`);
  }
}
