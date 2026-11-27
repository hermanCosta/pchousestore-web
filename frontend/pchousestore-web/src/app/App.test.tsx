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

  it("renders the authenticated Shop shell and the four operational destinations", async () => {
    await renderAuthenticatedApp();

    expect(screen.getByText("Dublin Central")).toBeInTheDocument();
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

  it("shows the approved Shop, Employee Settings and Session menu entries", async () => {
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
      expect(
        screen.getByRole("button", { name: label }),
      ).toBeInTheDocument();
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

      expect(
        screen.getByRole("button", { name: label }),
      ).toHaveAttribute("aria-current", "page");
      expect(
        screen.getByTestId("catalogue-administration"),
      ).toHaveTextContent(page);
    },
  );

  it("closes the administration menu after selecting a settings destination", async () => {
    const user = userEvent.setup();
    await renderAuthenticatedApp();
    await openAdministrationMenu(user);

    await user.click(
      screen.getByRole("button", { name: "Shop access credential" }),
    );

    expect(
      screen.queryByRole("navigation", { name: "Administration" }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Shop access credential" }),
    ).toBeInTheDocument();
  });

  it("closes the menu and initiates the existing OIDC sign-out flow", async () => {
    const user = userEvent.setup();
    await renderAuthenticatedApp();
    await openAdministrationMenu(user);

    await user.click(screen.getByRole("button", { name: "Sign out" }));

    await waitFor(() => expect(signoutRedirect).toHaveBeenCalledOnce());
    expect(
      screen.queryByRole("navigation", { name: "Administration" }),
    ).not.toBeInTheDocument();
  });
});
