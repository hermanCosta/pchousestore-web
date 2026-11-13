import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import EmployeeAdministration, { type EmployeeAdminPage } from "./EmployeeAdministration";
import ManagerCredentialsDialog, { type ManagerCredentials } from "./ManagerCredentialsDialog";
import { apiBaseUrl, getShopAccessToken, userManager } from "../auth/oidc";

let callbackProcessing: Promise<void> | undefined;

type ShopSession = {
  companyId: string;
  shopId: string;
  shopCode: string;
  shopName: string;
  shopAddress: string | null;
  shopPhone: string | null;
  companyEmail: string | null;
};
type AdminPage = "shop-profile" | "shop-access-credential" | EmployeeAdminPage;

const adminPageTitles: Record<AdminPage, string> = {
  "shop-profile": "Shop profile",
  "shop-access-credential": "Shop access credential",
  "employee-password-change": "Change my employee password",
  "employee-directory-search": "Load employees",
  "create-employee": "Create employee",
  "employee-directory": "Employee directory",
};
const adminPageDescriptions: Record<AdminPage, string> = {
  "shop-profile": "Your active Shop details.",
  "shop-access-credential": "Manage this Shop's sign-in credential.",
  "employee-password-change": "Change your employee credential. This is separate from the Shop sign-in password.",
  "employee-directory-search": "Search and load employee records for this Company.",
  "create-employee": "Register an employee and assign their Company-wide role.",
  "employee-directory": "Review and administer employee registration records.",
};

export default function App() {
  const [status, setStatus] = useState("Checking session…");
  const [error, setError] = useState<string | null>(null);
  const [shop, setShop] = useState<ShopSession | null>(null);
  const [action, setAction] = useState<"rotate" | "status">("rotate");
  const [managerDialogOpen, setManagerDialogOpen] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [activePage, setActivePage] = useState<AdminPage>("shop-profile");
  const [credentialVersion, setCredentialVersion] = useState("1");
  const [newShopPassword, setNewShopPassword] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [saving, setSaving] = useState(false);
  const [signingOut, setSigningOut] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    async function initialize() {
      try {
        if (window.location.pathname === "/auth/callback") {
          callbackProcessing ??= userManager.signinRedirectCallback().then(() => {
            window.history.replaceState({}, document.title, "/");
          });
          await callbackProcessing;
        } else if (window.location.pathname === "/auth/logout-callback") {
          callbackProcessing ??= userManager.signoutRedirectCallback().then(() => {
            window.history.replaceState({}, document.title, "/");
          });
          await callbackProcessing;
        }
        const token = await getShopAccessToken();
        if (!token) {
          if (active) {
            setShop(null);
            setStatus("No Shop is signed in");
          }
          return;
        }
        const response = await fetch(`${apiBaseUrl}/api/v1/session`, {
          headers: { Authorization: `Bearer ${token}` },
        });
        if (response.status === 401) throw new Error("The API rejected the access token (HTTP 401). Please sign in again.");
        if (response.status === 403) throw new Error("The access token is missing the required Shop API scope (HTTP 403).");
        if (!response.ok) throw new Error(`Session verification failed (HTTP ${response.status}).`);
        const session = await response.json() as ShopSession;
        if (active) {
          setShop(session);
          setCredentialVersion(window.sessionStorage.getItem(`pc-house-credential-version.${session.shopId}`) ?? "1");
          setStatus("Shop signed in");
        }
      } catch (cause) {
        if (active) setError(cause instanceof Error ? cause.message : "Sign-in failed.");
      }
    }
    void initialize();
    return () => { active = false; };
  }, []);

  async function handleCredentialSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!shop || saving) return;
    setError(null);
    setNotice(null);
    setManagerDialogOpen(true);
  }

  async function confirmShopChange(credentials: ManagerCredentials): Promise<boolean> {
    if (!shop || saving) return false;
    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      const token = await getShopAccessToken();
      if (!token) throw new Error("Your Shop session has expired. Sign in again.");
      const version = Number(credentialVersion);
      if (!Number.isSafeInteger(version) || version < 1) throw new Error("Enter a valid credential version.");

      const actionCode = action === "rotate" ? "SHOP_ACCESS_CREDENTIAL_ROTATE" : "SHOP_ACCESS_CREDENTIAL_CHANGE_STATUS";
      const challenge = await fetch(`${apiBaseUrl}/api/v1/employee-action-authorizations`, {
        method: "POST",
        headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify({
          employeeCode: credentials.employeeCode,
          password: credentials.password,
          actionCode,
          resourceType: "Shop",
          resourceId: shop.shopId,
          targetShopId: Number(shop.shopId),
          operationContext: { credentialVersion: version },
        }),
      });
      if (!challenge.ok) throw await apiError(challenge);
      const authorization = await challenge.json() as { authorizationToken: string };
      const response = await fetch(`${apiBaseUrl}/api/v1/shops/${encodeURIComponent(shop.shopId)}/access-credential`, {
        method: action === "rotate" ? "PUT" : "PATCH",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
          "X-Employee-Action-Authorization": authorization.authorizationToken,
        },
        body: JSON.stringify(action === "rotate"
          ? { newPassword: newShopPassword, expectedCredentialVersion: version }
          : { isActive, expectedCredentialVersion: version }),
      });
      if (!response.ok) throw await apiError(response);

      window.sessionStorage.setItem(`pc-house-credential-version.${shop.shopId}`, String(version + 1));
      setCredentialVersion(String(version + 1));
      setNewShopPassword("");
      setNotice("Credential updated. All sessions for this Shop were signed out; sign in again to continue.");
      await userManager.removeUser();
      setShop(null);
      setStatus("Shop credential updated");
      setManagerDialogOpen(false);
      return true;
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "The credential could not be updated.");
      return false;
    } finally {
      setNewShopPassword("");
      setSaving(false);
    }
  }

  function navigateAdmin(page: AdminPage) {
    setMenuOpen(false);
    setActivePage(page);
    setError(null);
    setNotice(null);
  }

  async function handleSignOut() {
    setError(null);
    setSigningOut(true);
    try {
      await userManager.signoutRedirect();
    } catch (cause) {
      setSigningOut(false);
      setError(cause instanceof Error ? cause.message : "Sign out could not be started.");
    }
  }

  if (window.location.pathname === "/auth/callback" || window.location.pathname === "/auth/logout-callback") {
    return (
      <main className="auth-shell">
        <section className="auth-card" aria-live="polite">
          <Brand />
          <div className="auth-content">
            <h1>{window.location.pathname === "/auth/callback" ? "Completing sign-in" : "Completing sign out"}</h1>
            <p className="auth-description">{window.location.pathname === "/auth/callback" ? "Please wait while your Shop session is verified." : "Please wait while your Shop session is cleared."}</p>
            {error && (
              <div className="auth-error" role="alert">
                <p>{error}</p>
                <button onClick={() => window.location.assign("/")}>{window.location.pathname === "/auth/callback" ? "Restart sign-in" : "Return to Shop access"}</button>
              </div>
            )}
          </div>
        </section>
      </main>
    );
  }

  return (
    <main className="auth-shell">
      <section className={shop ? "auth-card auth-card-wide" : "auth-card"}>
        <div className="app-header"><Brand />{shop && <div className="admin-menu">
          <button className="admin-menu-trigger" aria-label="Open administration menu" aria-expanded={menuOpen} onClick={() => setMenuOpen(value => !value)}><span/><span/><span/></button>
          {menuOpen && <nav className="admin-menu-panel" aria-label="Administration">
            <p>SHOP</p><button className="admin-menu-item" aria-current={activePage === "shop-profile" ? "page" : undefined} onClick={() => navigateAdmin("shop-profile")}>Shop profile</button><button className="admin-menu-item" aria-current={activePage === "shop-access-credential" ? "page" : undefined} onClick={() => navigateAdmin("shop-access-credential")}>Shop access credential</button>
            <p>EMPLOYEES</p><button className="admin-menu-item" aria-current={activePage === "employee-password-change" ? "page" : undefined} onClick={() => navigateAdmin("employee-password-change")}>Change my password</button><button className="admin-menu-item" aria-current={activePage === "employee-directory-search" ? "page" : undefined} onClick={() => navigateAdmin("employee-directory-search")}>Load employees</button><button className="admin-menu-item" aria-current={activePage === "create-employee" ? "page" : undefined} onClick={() => navigateAdmin("create-employee")}>Create employee</button><button className="admin-menu-item" aria-current={activePage === "employee-directory" ? "page" : undefined} onClick={() => navigateAdmin("employee-directory")}>Employee directory</button>
            <p>SESSION</p><button className="admin-menu-item" disabled={signingOut} onClick={() => { setMenuOpen(false); void handleSignOut(); }}>{signingOut ? "Signing out…" : "Sign out"}</button>
          </nav>}
        </div>}</div>
        <div className="auth-content">
          <h1>{shop ? adminPageTitles[activePage] : "Shop access"}</h1>
          <p className="auth-description">
            {shop
              ? adminPageDescriptions[activePage]
              : "Sign in to access your Shop workspace."}
          </p>
          <p className="auth-status" aria-live="polite">
            {shop ? `Signed in to ${shop.shopName} (${shop.shopCode})` : status}
          </p>
          {shop && activePage === "shop-profile" && (
            <dl id="shop-profile" className="shop-profile" aria-label="Active Shop profile">
              <div><dt>Shop</dt><dd>{shop.shopName}</dd></div>
              <div><dt>Address</dt><dd>{shop.shopAddress || "Not set"}</dd></div>
              <div><dt>Phone</dt><dd>{shop.shopPhone || "Not set"}</dd></div>
              <div><dt>Email</dt><dd>{shop.companyEmail || "Not set"}</dd></div>
            </dl>
          )}
          {error && <p className="auth-error" role="alert">{error}</p>}
          {notice && <p className="auth-notice" role="status">{notice}</p>}
          {shop && activePage === "shop-access-credential" && (
            <form id="shop-access-credential" className="auth-form credential-form" onSubmit={handleCredentialSubmit}>
              <h2>Shop access credential</h2>
              <p className="form-help">Changes apply to {shop.shopName}. You will need to sign in again after saving.</p>
              <label className="auth-field">
                Credential version
                <input type="number" min="1" step="1" required value={credentialVersion} onChange={event => setCredentialVersion(event.target.value)} />
              </label>
              <p className="form-help">This version is remembered in this browser after a successful update. If another Manager changed the credential, enter its current version and request fresh verification.</p>
              <label className="auth-field">
                Operation
                <select value={action} onChange={event => setAction(event.target.value as "rotate" | "status")}>
                  <option value="rotate">Change Shop password</option>
                  <option value="status">Change credential status</option>
                </select>
              </label>
              {action === "rotate" ? (
                <label className="auth-field">
                  New Shop password
                  <input type="password" minLength={12} maxLength={128} autoComplete="new-password" required value={newShopPassword} onChange={event => setNewShopPassword(event.target.value)} />
                </label>
              ) : (
                <>
                  <label className="auth-field">
                    Status
                    <select value={isActive ? "active" : "disabled"} onChange={event => setIsActive(event.target.value === "active")}>
                      <option value="active">Enabled</option>
                      <option value="disabled">Disabled</option>
                    </select>
                  </label>
                </>
              )}
              <button className="auth-submit" disabled={saving}>{saving ? "Saving…" : "Save credential"}</button>
            </form>
          )}
          {shop && activePage !== "shop-profile" && activePage !== "shop-access-credential" &&
            <EmployeeAdministration page={activePage} companyId={shop.companyId} getShopAccessToken={getShopAccessToken} />}
          <div className="auth-actions">
            {!shop && <button onClick={() => void userManager.signinRedirect()}>Sign in</button>}
          </div>
          <ManagerCredentialsDialog open={managerDialogOpen} busy={saving} error={error} onCancel={() => { setManagerDialogOpen(false); setError(null); }} onSubmit={confirmShopChange} />
        </div>
      </section>
    </main>
  );
}

async function apiError(response: Response): Promise<Error> {
  try {
    const problem = await response.json() as { detail?: string; code?: string };
    return new Error(problem.detail ?? problem.code ?? `Request failed (HTTP ${response.status}).`);
  } catch {
    return new Error(`Request failed (HTTP ${response.status}).`);
  }
}

function Brand() {
  return (
    <div className="auth-brand">
      <img src={`${apiBaseUrl}/assets/pc-house-logo.png`} alt="PC House logo" />
      <span>PC HOUSE STORE</span>
    </div>
  );
}
