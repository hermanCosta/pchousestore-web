import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import App from "./App";

const { getShopAccessToken, signoutRedirect } = vi.hoisted(() => ({
  getShopAccessToken: vi.fn(),
  signoutRedirect: vi.fn(),
}));

vi.mock("../auth/oidc", () => ({
  apiBaseUrl: "https://api.test",
  getShopAccessToken,
  userManager: {
    events: {
      addSilentRenewError: vi.fn(() => vi.fn()),
    },
    signoutRedirect,
    removeUser: vi.fn(),
    signinRedirect: vi.fn(),
  },
}));

vi.mock("./EmployeeAdministration", () => ({
  default: ({ page }: { page: string }) => (
    <div data-testid="employee-administration">{page}</div>
  ),
}));

vi.mock("./CatalogueAdministration", () => ({
  default: ({ page }: { page: string }) => (
    <div data-testid="catalogue-administration">{page}</div>
  ),
}));

vi.mock("./ManagerCredentialsDialog", () => ({
  default: () => null,
}));

const shopSession = {
  companyId: "1",
  shopId: "10",
  shopCode: "DUB01",
  shopName: "Dublin Central",
  shopAddress: "12 Main Street, Dublin",
  shopPhone: "+353 1 234 5678",
  companyEmail: "hello@example.ie",
};

async function renderAuthenticatedApp() {
  getShopAccessToken.mockResolvedValue("shop-access-token");
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => shopSession,
    }),
  );

  render(<App />);
  await screen.findByRole("navigation", { name: "Main navigation" });
}

async function openAdministrationMenu(
  user: ReturnType<typeof userEvent.setup>,
) {
  await user.click(
    screen.getByRole("button", { name: "Open administration menu" }),
  );
}

describe("Application shell navigation", () => {
  beforeEach(() => {
    getShopAccessToken.mockReset();
    signoutRedirect.mockReset().mockResolvedValue(undefined);
    window.history.replaceState({}, "", "/");
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("renders Shop shell and operational destinations", async () => {
    await renderAuthenticatedApp();

    expect(
      screen
        .getAllByText("Dublin Central")
        .some((element) => element.closest(".header-shop-identity")),
    ).toBe(true);
    expect(
      screen
        .getAllByText("12 Main Street, Dublin")
        .some((element) => element.closest(".header-shop-identity")),
    ).toBe(true);
    expect(
      screen.getByRole("img", {
        name: "PC HOUSE — SELLS AND REPAIRS LAPTOPS",
      }),
    ).toHaveAttribute("src", "/assets/pc-house-wordmark.webp");
    expect(
      screen.getByRole("navigation", { name: "Main navigation" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Customers" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Products & Services" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Categories" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Fault catalogue" }),
    ).toBeInTheDocument();
    expect(screen.queryByPlaceholderText(/search/i)).not.toBeInTheDocument();
  });

  it("shows approved administration menu entries", async () => {
    const user = userEvent.setup();
    await renderAuthenticatedApp();
    await openAdministrationMenu(user);

    const menu = screen.getByRole("navigation", { name: "Administration" });
    expect(menu).toHaveTextContent("SHOP");
    expect(menu).toHaveTextContent("EMPLOYEE SETTINGS");
    expect(menu).toHaveTextContent("SESSION");

    for (const label of [
      "Shop profile",
      "Shop access credential",
      "Change my password",
      "Load employees",
      "Create employee",
      "Employee directory",
      "Sign out",
    ]) {
      expect(screen.getByRole("button", { name: label })).toBeInTheDocument();
    }
  });

  it.each([
    ["Customers", "customer-directory"],
    ["Products & Services", "catalog-items"],
    ["Categories", "product-categories"],
    ["Fault catalogue", "fault-catalogue"],
  ])(
    "navigates to %s and marks it as the current page",
    async (label, page) => {
      const user = userEvent.setup();
      await renderAuthenticatedApp();

      await user.click(screen.getByRole("button", { name: label }));

      expect(screen.getByRole("button", { name: label })).toHaveAttribute(
        "aria-current",
        "page",
      );
      expect(screen.getByTestId("catalogue-administration")).toHaveTextContent(
        page,
      );
    },
  );

  it("closes menu after selecting a settings destination", async () => {
    const user = userEvent.setup();
    await renderAuthenticatedApp();
    await openAdministrationMenu(user);

    await user.click(
      screen.getByRole("button", { name: "Shop access credential" }),
    );

    expect(
      screen.queryByRole("navigation", { name: "Administration" }),
    ).not.toBeInTheDocument();
    const credentialForm = document.getElementById("shop-access-credential");
    expect(credentialForm).toBeInTheDocument();
    expect(credentialForm?.querySelector("h2")).toHaveTextContent(
      "Shop access credential",
    );
  });

  it("closes menu and initiates OIDC sign-out", async () => {
    const user = userEvent.setup();
    await renderAuthenticatedApp();
    await openAdministrationMenu(user);

    await user.click(screen.getByRole("button", { name: "Sign out" }));

    await waitFor(() => expect(signoutRedirect).toHaveBeenCalledOnce());
    expect(
      screen.queryByRole("navigation", { name: "Administration" }),
    ).not.toBeInTheDocument();
  });

  it("shows the sign-in state and starts OIDC sign-in", async () => {
    const user = userEvent.setup();
    getShopAccessToken.mockResolvedValue(null);
    render(<App />);

    expect(await screen.findByText("No Shop is signed in")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Sign in" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("navigation", { name: "Main navigation" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Open administration menu" }),
    ).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Sign in" }));
    const { userManager } = await import("../auth/oidc");
    expect(userManager.signinRedirect).toHaveBeenCalledOnce();
  });

  it.each([
    [
      401,
      "The API rejected the access token (HTTP 401). Please sign in again.",
    ],
    [
      403,
      "The access token is missing the required Shop API scope (HTTP 403).",
    ],
    [500, "Session verification failed (HTTP 500)."],
  ])(
    "reports session verification failure for HTTP %s",
    async (status, message) => {
      getShopAccessToken.mockResolvedValue("shop-access-token");
      vi.stubGlobal(
        "fetch",
        vi.fn().mockResolvedValue({ ok: false, status }),
      );

      render(<App />);

      expect(await screen.findByRole("alert")).toHaveTextContent(message);
      expect(
        screen.queryByRole("navigation", { name: "Main navigation" }),
      ).not.toBeInTheDocument();
    },
  );

  it("uses fallback values for optional Shop profile fields", async () => {
    getShopAccessToken.mockResolvedValue("shop-access-token");
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({
          ...shopSession,
          shopAddress: null,
          shopPhone: null,
          companyEmail: null,
        }),
      }),
    );

    render(<App />);

    expect(
      await screen.findByRole("navigation", { name: "Main navigation" }),
    ).toBeInTheDocument();
    const profile = document.getElementById("shop-profile");
    expect(profile).toHaveTextContent("Not set");
  });

  it.each([
    ["Change my password", "employee-password-change"],
    ["Load employees", "employee-directory-search"],
    ["Create employee", "create-employee"],
    ["Employee directory", "employee-directory"],
  ])(
    "navigates to employee destination %s",
    async (label, page) => {
      const user = userEvent.setup();
      await renderAuthenticatedApp();
      await openAdministrationMenu(user);

      await user.click(screen.getByRole("button", { name: label }));

      expect(
        screen.queryByRole("navigation", { name: "Administration" }),
      ).not.toBeInTheDocument();
      expect(screen.getByTestId("employee-administration")).toHaveTextContent(
        page,
      );
    },
  );

  it("updates the hamburger expanded state when the menu is toggled", async () => {
    const user = userEvent.setup();
    await renderAuthenticatedApp();

    const trigger = screen.getByRole("button", {
      name: "Open administration menu",
    });
    expect(trigger).toHaveAttribute("aria-expanded", "false");

    await user.click(trigger);
    expect(trigger).toHaveAttribute("aria-expanded", "true");
    expect(
      screen.getByRole("navigation", { name: "Administration" }),
    ).toBeInTheDocument();

    await user.click(trigger);
    expect(trigger).toHaveAttribute("aria-expanded", "false");
    expect(
      screen.queryByRole("navigation", { name: "Administration" }),
    ).not.toBeInTheDocument();
  });
});
