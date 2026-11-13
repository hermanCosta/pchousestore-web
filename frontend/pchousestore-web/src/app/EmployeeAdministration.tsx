import { useState, type FormEvent } from "react";
import { apiBaseUrl } from "../auth/oidc";
import ManagerCredentialsDialog, { type ManagerCredentials } from "./ManagerCredentialsDialog";

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
export type EmployeeAdminPage = "employee-password-change" | "employee-directory-search" | "create-employee" | "employee-directory";
type Props = ShopContext & { page: EmployeeAdminPage; getShopAccessToken: () => Promise<string | null> };
type Profile = Pick<Employee, "firstName" | "lastName" | "phone" | "email">;
type PendingManagerAction = { key: string; action: (credentials: ManagerCredentials) => Promise<void> };

export default function EmployeeAdministration({ companyId, page, getShopAccessToken }: Props) {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [directoryLoaded, setDirectoryLoaded] = useState(false);
  const [search, setSearch] = useState("");
  const [newEmployee, setNewEmployee] = useState({ employeeCode: "", firstName: "", lastName: "", phone: "", email: "", initialPassword: "", roles: ["TECHNICIAN"] });
  const [initialPasswordCustomized, setInitialPasswordCustomized] = useState(false);
  const [selfChange, setSelfChange] = useState({ employeeCode: "", currentPassword: "", newPassword: "" });
  const [busyAction, setBusyAction] = useState<string | null>(null);
  const [managerDialogOpen, setManagerDialogOpen] = useState(false);
  const [pendingManagerAction, setPendingManagerAction] = useState<PendingManagerAction | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function managerRequest<T>(
    actionCode: string,
    resourceType: "Company" | "Employee",
    resourceId: string,
    path: string,
    method: string,
    body: unknown,
    credentials: ManagerCredentials,
  ): Promise<T | null> {
    const token = await getShopAccessToken();
    if (!token) throw new Error("Your Shop session has expired. Sign in again.");
    const challenge = await fetch(`${apiBaseUrl}/api/v1/employee-action-authorizations`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
      body: JSON.stringify({
        employeeCode: credentials.employeeCode,
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
    await executeAction("employee-directory", async () => {
      const token = await getShopAccessToken();
      if (!token) throw new Error("Your Shop session has expired. Sign in again.");
      const result = await fetch(`${apiBaseUrl}/api/v1/employees/queries`, {
        method: "POST",
        headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify({ search }),
      });
      if (!result.ok) throw await responseError(result);
      const response = await result.json() as { items: Employee[] };
      setEmployees(response?.items ?? []);
      setDirectoryLoaded(true);
      setNotice("Employee directory loaded.");
    });
  }

  async function createEmployee(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    requestManagerAction("employee-create", async credentials => {
      const response = await managerRequest<Employee>(
        "EMPLOYEE_CREATE", "Company", companyId, "/api/v1/employees", "POST", newEmployee,
        credentials,
      );
      if (response) setEmployees(current => [...current, response].sort((left, right) => left.employeeCode.localeCompare(right.employeeCode)));
      setNewEmployee({ employeeCode: "", firstName: "", lastName: "", phone: "", email: "", initialPassword: "", roles: ["TECHNICIAN"] });
      setInitialPasswordCustomized(false);
      setNotice("Employee created. Share the initial password securely; the employee must change it before their first protected action.");
    });
  }

  async function updateProfile(event: FormEvent<HTMLFormElement>, employee: Employee, profile: Profile) {
    event.preventDefault();
    requestManagerAction(`profile-${employee.employeeId}`, async credentials => {
      await managerRequest<never>("EMPLOYEE_PROFILE_UPDATE", "Employee", employee.employeeId,
        `/api/v1/employees/${encodeURIComponent(employee.employeeId)}`, "PUT", profile,
        credentials);
      setEmployees(current => current.map(item => item.employeeId === employee.employeeId ? { ...item, ...profile } : item));
      setNotice(`Updated ${employee.employeeCode}.`);
    });
  }

  async function updateStatus(employee: Employee) {
    requestManagerAction(`status-${employee.employeeId}`, async credentials => {
      await managerRequest<never>("EMPLOYEE_STATUS_CHANGE", "Employee", employee.employeeId,
        `/api/v1/employees/${encodeURIComponent(employee.employeeId)}/status`, "PATCH", { isActive: !employee.isActive },
        credentials);
      setEmployees(current => current.map(item => item.employeeId === employee.employeeId ? { ...item, isActive: !item.isActive } : item));
      setNotice(`${employee.employeeCode} ${employee.isActive ? "deactivated" : "reactivated"}.`);
    });
  }

  async function updateRoles(employee: Employee, roles: string[]) {
    requestManagerAction(`roles-${employee.employeeId}`, async credentials => {
      await managerRequest<never>("EMPLOYEE_ROLE_CHANGE", "Employee", employee.employeeId,
        `/api/v1/employees/${encodeURIComponent(employee.employeeId)}/roles`, "PUT", { roles },
        credentials);
      setEmployees(current => current.map(item => item.employeeId === employee.employeeId ? { ...item, roles } : item));
      setNotice(`Updated roles for ${employee.employeeCode}.`);
    });
  }

  async function resetCredential(event: FormEvent<HTMLFormElement>, employee: Employee, newPassword: string) {
    event.preventDefault();
    requestManagerAction(`reset-${employee.employeeId}`, async credentials => {
      await managerRequest<never>("EMPLOYEE_CREDENTIAL_RESET", "Employee", employee.employeeId,
        `/api/v1/employees/${encodeURIComponent(employee.employeeId)}/credential-reset`, "POST", { newPassword },
        credentials);
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
        { employeeCode: selfChange.employeeCode, password: selfChange.currentPassword });
      setSelfChange({ employeeCode: "", currentPassword: "", newPassword: "" });
      setNotice("Your employee password has been changed.");
    });
  }

  function requestManagerAction(key: string, action: PendingManagerAction["action"]) {
    setError(null);
    setNotice(null);
    setPendingManagerAction({ key, action });
    setManagerDialogOpen(true);
  }

  async function executeAction(key: string, action: () => Promise<void>): Promise<boolean> {
    setBusyAction(key);
    setError(null);
    setNotice(null);
    try {
      await action();
      return true;
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "The employee operation failed.");
      return false;
    } finally {
      setNewEmployee(current => ({ ...current, initialPassword: "" }));
      setSelfChange(current => ({ ...current, currentPassword: "", newPassword: "" }));
      setBusyAction(null);
    }
  }

  function runAction(key: string, action: () => Promise<void>) {
    void executeAction(key, action);
  }

  async function confirmManager(credentials: ManagerCredentials): Promise<boolean> {
    if (!pendingManagerAction) return false;
    const pending = pendingManagerAction;
    const completed = await executeAction(pending.key, () => pending.action(credentials));
    if (completed) {
      setManagerDialogOpen(false);
      setPendingManagerAction(null);
    }
    return completed;
  }

  function cancelManagerPrompt() {
    setManagerDialogOpen(false);
    setPendingManagerAction(null);
    setBusyAction(null);
    setNewEmployee(current => ({ ...current, initialPassword: "" }));
    setInitialPasswordCustomized(false);
    setError(null);
  }

  function toggleNewRole(role: string) {
    setNewEmployee(current => ({
      ...current,
      roles: current.roles.includes(role) ? current.roles.filter(value => value !== role) : [...current.roles, role],
    }));
  }

  return (
    <section id="employee-admin" className="employee-admin" aria-label="Employee administration page">
      {error && <p className="auth-error" role="alert">{error}</p>}
      {notice && <p className="auth-notice" role="status">{notice}</p>}

      {page === "employee-password-change" && <form id="employee-password-change" className="auth-form employee-password-change" onSubmit={changeOwnPassword}>
        <p className="form-help">Do this first if your employee password was just created or reset. This changes your employee password, not the Shop sign-in password. A password change is required before you can manage employees.</p>
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
      </form>}

      {page === "employee-directory-search" && <form id="employee-directory-search" className="auth-form employee-action-form" onSubmit={loadDirectory}>
        <p className="form-help">Search and view employee records. Manager credentials are requested when you load the directory.</p>
        <div className="employee-form-grid">
          <label className="auth-field">Search employees
            <input maxLength={200} value={search} onChange={event => setSearch(event.target.value)} placeholder="Name, code, phone or email" />
          </label>
        </div>
        <button disabled={busyAction !== null}>{busyAction === "employee-directory" ? "Loading…" : "Load employees"}</button>
      </form>}

      {page === "create-employee" && <form id="create-employee" className="auth-form employee-create-form" onSubmit={createEmployee}>
        <div className="employee-form-grid">
          <label className="auth-field">Employee code
            <input required maxLength={40} value={newEmployee.employeeCode} onChange={event => setNewEmployee(current => ({ ...current, employeeCode: event.target.value, initialPassword: initialPasswordCustomized ? current.initialPassword : defaultTemporaryPassword(event.target.value) }))} />
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
            <input type="password" required minLength={12} maxLength={128} autoComplete="new-password" value={newEmployee.initialPassword} onChange={event => { setInitialPasswordCustomized(true); setNewEmployee(current => ({ ...current, initialPassword: event.target.value })); }} />
          </label>
        </div>
        <p className="form-help">Default: employee code + @pchouse. The password must be at least 12 characters; edit it if the generated value is shorter. The employee must change it before performing any protected action.</p>
        <RoleChecks roles={newEmployee.roles} onToggle={toggleNewRole} />
        <p className="form-help">Manager credentials will be requested when you create this employee.</p>
        <button disabled={busyAction !== null}>{busyAction === "employee-create" ? "Creating…" : "Create employee"}</button>
      </form>}

      {page === "employee-directory" && directoryLoaded && (
        <div id="employee-directory" className="employee-directory">
          <h3>Employees ({employees.length})</h3>
          {employees.length === 0 && <p className="form-help">No employees match this search.</p>}
          <div className="employee-list">
            {employees.map(employee => <EmployeeCard key={employee.employeeId} employee={employee} busy={busyAction !== null}
              onUpdateProfile={updateProfile} onUpdateStatus={updateStatus} onUpdateRoles={updateRoles} onResetCredential={resetCredential} />)}
          </div>
        </div>
      )}
      {page === "employee-directory" && !directoryLoaded && <p className="form-help">No employee records loaded yet. Use “Load employees” from the administration menu first.</p>}
      <ManagerCredentialsDialog open={managerDialogOpen} busy={busyAction !== null} error={error}
        onCancel={cancelManagerPrompt} onSubmit={confirmManager} />
    </section>
  );
}

function EmployeeCard({
  employee,
  busy,
  onUpdateProfile,
  onUpdateStatus,
  onUpdateRoles,
  onResetCredential,
}: {
  employee: Employee;
  busy: boolean;
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
          <button disabled={busy}>Save profile</button>
        </form>
        <form className="auth-form" onSubmit={event => { event.preventDefault(); void onUpdateRoles(employee, roles); }}>
          <h4>Company-wide roles</h4>
          <RoleChecks roles={roles} onToggle={toggleRole} />
          <button disabled={busy}>Save roles</button>
        </form>
        <form className="auth-form" onSubmit={event => void onResetCredential(event, employee, resetPassword).finally(() => setResetPassword(""))}>
          <h4>Reset employee password</h4>
          <label className="auth-field">New temporary password<input type="password" required minLength={12} maxLength={128} autoComplete="new-password" value={resetPassword} onChange={event => setResetPassword(event.target.value)} /></label>
          <button disabled={busy}>Reset password</button>
        </form>
        <div className="employee-status-action">
          <p>{employee.isActive ? "Deactivate this employee to prevent future employee challenges." : "Reactivate this employee to allow employee challenges again."}</p>
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

async function responseError(response: Response): Promise<Error> {
  try {
    const body = await response.json() as { detail?: string; code?: string };
    if (body.code === "employee_password_change_required")
      return new Error("This employee password was just created or reset. First use “Change my employee password” above. Shop access credentials are separate.");
    return new Error(body.detail ?? body.code ?? `Request failed (HTTP ${response.status}).`);
  } catch {
    return new Error(`Request failed (HTTP ${response.status}).`);
  }
}

function defaultTemporaryPassword(employeeCode: string) {
  const normalizedCode = employeeCode.trim().toUpperCase();
  return normalizedCode ? `${normalizedCode}@pchouse` : "";
}
