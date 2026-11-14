import { useCallback, useEffect, useState, type FormEvent } from "react";
import { apiBaseUrl } from "../auth/oidc";

export type CatalogueAdminPage = "customer-directory" | "catalog-items" | "product-categories" | "fault-catalogue";
type Props = { page: CatalogueAdminPage; getShopAccessToken: () => Promise<string | null> };
type Customer = { id: string; firstName: string; lastName: string; phone: string; phoneE164: string; email: string | null; isActive: boolean };
type CatalogItem = { id: string; kind: "PRODUCT" | "SERVICE"; name: string; sku: string | null; listPrice: number; productCategoryId: string | null; inventoryTrackingMode: string | null; description: string | null; isActive: boolean };
type Category = { id: string; name: string; code: string; parentCategoryId: string | null; parentCategoryName: string | null; description: string | null; isActive: boolean };
type Fault = { id: string; code: string; description: string; isActive: boolean };
type PageResponse<T> = { items: T[]; total: number; page: number; pageSize: number };
type PendingAction = { method: "POST" | "PUT"; path: string; body?: unknown; success: string };

const headings: Record<CatalogueAdminPage, string> = {
  "customer-directory": "Customer directory",
  "catalog-items": "Product and service catalogue",
  "product-categories": "Product categories",
  "fault-catalogue": "Fault catalogue",
};

export default function CatalogueAdministration({ page, getShopAccessToken }: Props) {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [items, setItems] = useState<CatalogItem[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [faults, setFaults] = useState<Fault[]>([]);
  const [search, setSearch] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false);
  const [customerForm, setCustomerForm] = useState({ firstName: "", lastName: "", phone: "", isInternational: false, email: "" });
  const [itemForm, setItemForm] = useState({ kind: "PRODUCT", name: "", sku: "", listPrice: "", productCategoryId: "", inventoryTrackingMode: "QUANTITY", description: "" });
  const [categoryForm, setCategoryForm] = useState({ name: "", code: "", parentCategoryId: "", description: "" });
  const [faultDescription, setFaultDescription] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const token = await requireToken(getShopAccessToken);
      const endpoint = page === "customer-directory" ? "customers" : page === "catalog-items" ? "catalog-items" : page === "product-categories" ? "product-categories" : "faults";
      const response = await fetch(`${apiBaseUrl}/api/v1/${endpoint}/queries`, {
        method: "POST",
        headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify({ search, includeInactive, page: 1, pageSize: 100 }),
      });
      if (!response.ok) throw await responseError(response);
      const result = await response.json() as PageResponse<Customer | CatalogItem | Category | Fault>;
      if (page === "customer-directory") setCustomers(result.items as Customer[]);
      else if (page === "catalog-items") {
        setItems(result.items as CatalogItem[]);
        const categoryResponse = await fetch(`${apiBaseUrl}/api/v1/product-categories/queries`, {
          method: "POST",
          headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
          body: JSON.stringify({ includeInactive: true, page: 1, pageSize: 100 }),
        });
        if (!categoryResponse.ok) throw await responseError(categoryResponse);
        const categoryResult = await categoryResponse.json() as PageResponse<Category>;
        setCategories(categoryResult.items);
      }
      else if (page === "product-categories") setCategories(result.items as Category[]);
      else setFaults(result.items as Fault[]);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "The records could not be loaded.");
    } finally {
      setLoading(false);
    }
  }, [getShopAccessToken, includeInactive, page, search]);

  useEffect(() => { void load(); }, [load]);

  function queue(action: PendingAction) {
    setError(null);
    setNotice(null);
    void submitPending(action);
  }

  async function saveCustomer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const id = editingId;
    queue({
      method: id ? "PUT" : "POST",
      path: id ? `/customers/${id}` : "/customers",
      body: { ...customerForm, email: customerForm.email.trim() || null },
      success: id ? "Customer updated." : "Customer created.",
    });
  }

  async function saveCatalogItem(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const id = editingId;
    queue({
      method: id ? "PUT" : "POST",
      path: id ? `/catalog-items/${id}` : "/catalog-items",
      body: {
        ...itemForm,
        sku: itemForm.sku.trim() || null,
        listPrice: Number(itemForm.listPrice),
        productCategoryId: itemForm.kind === "PRODUCT" && itemForm.productCategoryId ? itemForm.productCategoryId : null,
        inventoryTrackingMode: itemForm.kind === "PRODUCT" ? itemForm.inventoryTrackingMode : null,
        description: itemForm.kind === "SERVICE" ? itemForm.description : null,
      },
      success: id ? "Catalogue item updated." : "Catalogue item created.",
    });
  }

  async function saveCategory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const id = editingId;
    queue({
      method: id ? "PUT" : "POST",
      path: id ? `/product-categories/${id}` : "/product-categories",
      body: { ...categoryForm, parentCategoryId: categoryForm.parentCategoryId || null, description: categoryForm.description.trim() || null },
      success: id ? "Product category updated." : "Product category created.",
    });
  }

  async function saveFault(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const id = editingId;
    queue({
      method: id ? "PUT" : "POST",
      path: id ? `/faults/${id}` : "/faults",
      body: { description: faultDescription },
      success: id ? "Fault updated." : "Fault created.",
    });
  }

  async function submitPending(pending: PendingAction): Promise<boolean> {
    if (busy) return false;
    setBusy(true);
    setError(null);
    try {
      const token = await requireToken(getShopAccessToken);
      const response = await fetch(`${apiBaseUrl}/api/v1${pending.path}`, {
        method: pending.method,
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json",
        },
        ...(pending.method === "POST" || pending.method === "PUT" ? { body: JSON.stringify(pending.body ?? {}) } : {}),
      });
      if (!response.ok) throw await responseError(response);
      setNotice(pending.success);
      resetForm();
      await load();
      return true;
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "The change could not be saved.");
      return false;
    } finally {
      setBusy(false);
    }
  }

  function resetForm() {
    setEditingId(null);
    setCustomerForm({ firstName: "", lastName: "", phone: "", isInternational: false, email: "" });
    setItemForm({ kind: "PRODUCT", name: "", sku: "", listPrice: "", productCategoryId: "", inventoryTrackingMode: "QUANTITY", description: "" });
    setCategoryForm({ name: "", code: "", parentCategoryId: "", description: "" });
    setFaultDescription("");
  }

  function editCustomer(customer: Customer) {
    setEditingId(customer.id);
    setCustomerForm({ firstName: customer.firstName, lastName: customer.lastName, phone: customer.phone, isInternational: customer.phone.startsWith("+"), email: customer.email ?? "" });
  }
  function editItem(item: CatalogItem) {
    setEditingId(item.id);
    setItemForm({ kind: item.kind, name: item.name, sku: item.sku ?? "", listPrice: String(item.listPrice), productCategoryId: item.productCategoryId ?? "", inventoryTrackingMode: item.inventoryTrackingMode ?? "QUANTITY", description: item.description ?? "" });
  }
  function editCategory(category: Category) {
    setEditingId(category.id);
    setCategoryForm({ name: category.name, code: category.code, parentCategoryId: category.parentCategoryId ?? "", description: category.description ?? "" });
  }
  function editFault(fault: Fault) { setEditingId(fault.id); setFaultDescription(fault.description); }

  function requestStatus(resource: "Customer" | "CatalogItem" | "ProductCategory" | "Fault", id: string, isActive: boolean) {
    const root = resource === "Customer" ? "customers" : resource === "CatalogItem" ? "catalog-items" : resource === "ProductCategory" ? "product-categories" : "faults";
    queue({
      method: "POST",
      path: `/${root}/${id}/${isActive ? "reactivate" : "deactivate"}`,
      success: `${resource === "CatalogItem" ? "Catalogue item" : resource === "ProductCategory" ? "Product category" : resource} ${isActive ? "reactivated" : "deactivated"}.`,
    });
  }

  return (
    <section className="catalogue-admin" aria-label={`${headings[page]} administration`}>
      {error && <p className="auth-error" role="alert">{error}</p>}
      {notice && <p className="auth-notice" role="status">{notice}</p>}
      <div className="catalogue-toolbar">
        <label className="auth-field">Search {page === "customer-directory" ? "customers" : page === "catalog-items" ? "Products and Services" : page === "product-categories" ? "categories" : "fault descriptions"}
          <input value={search} maxLength={200} onChange={event => setSearch(event.target.value)} onKeyDown={event => { if (event.key === "Enter") { event.preventDefault(); void load(); } }} />
        </label>
        <label className="catalogue-inactive-filter"><input type="checkbox" checked={includeInactive} onChange={event => setIncludeInactive(event.target.checked)} /> Include inactive</label>
        <button type="button" className="secondary" onClick={() => void load()} disabled={loading}>{loading ? "Loading…" : "Search"}</button>
      </div>

      {page === "customer-directory" && <>
        <form className="catalogue-form" onSubmit={saveCustomer}>
          <h2>{editingId ? "Edit customer" : "Create customer"}</h2>
          <div className="employee-form-grid">
            <label className="auth-field">First name<input required maxLength={200} value={customerForm.firstName} onChange={event => setCustomerForm(x => ({ ...x, firstName: event.target.value }))} /></label>
            <label className="auth-field">Last name<input required maxLength={200} value={customerForm.lastName} onChange={event => setCustomerForm(x => ({ ...x, lastName: event.target.value }))} /></label>
            <label className="auth-field">Phone<input required value={customerForm.phone} onChange={event => setCustomerForm(x => ({ ...x, phone: event.target.value }))} placeholder={customerForm.isInternational ? "+353 1 234 5678" : "01 234 5678"} /></label>
            <label className="auth-field">Email (optional)<input type="email" maxLength={320} value={customerForm.email} onChange={event => setCustomerForm(x => ({ ...x, email: event.target.value }))} /></label>
          </div>
          <label className="catalogue-checkbox"><input type="checkbox" checked={customerForm.isInternational} onChange={event => setCustomerForm(x => ({ ...x, isInternational: event.target.checked }))} /> International number</label>
          <p className="form-help">Irish phone numbers are assumed unless International number is selected. A phone number can belong to only one customer in this Company.</p>
          <div className="catalogue-form-actions"><button>{editingId ? "Save customer" : "Create customer"}</button>{editingId && <button type="button" className="secondary" onClick={resetForm}>Cancel edit</button>}</div>
        </form>
        <div className="catalogue-results"><h2>Customers</h2>{customers.length === 0 ? <p className="form-help">No customers found.</p> : customers.map(customer => <article className="catalogue-card" key={customer.id}>
          <div><strong>{customer.firstName} {customer.lastName}</strong><span>{customer.phone}</span><span>{customer.email || "No email"}</span>{!customer.isActive && <em>Inactive</em>}</div>
          <div className="catalogue-card-actions"><button type="button" className="secondary" onClick={() => editCustomer(customer)}>Edit</button><button type="button" className="secondary" onClick={() => requestStatus("Customer", customer.id, !customer.isActive)}>{customer.isActive ? "Deactivate" : "Reactivate"}</button></div>
        </article>)}</div>
      </>}

      {page === "catalog-items" && <>
        <form className="catalogue-form" onSubmit={saveCatalogItem}>
          <h2>{editingId ? "Edit catalogue item" : "Create Product or Service"}</h2>
          <div className="employee-form-grid">
            <label className="auth-field">Type<select value={itemForm.kind} onChange={event => setItemForm(x => ({ ...x, kind: event.target.value }))}><option value="PRODUCT">Product</option><option value="SERVICE">Service</option></select></label>
            <label className="auth-field">Name<input required maxLength={200} value={itemForm.name} onChange={event => setItemForm(x => ({ ...x, name: event.target.value }))} /></label>
            <label className="auth-field">SKU (optional)<input maxLength={12} placeholder="SKU-AB12CD34" value={itemForm.sku} onChange={event => setItemForm(x => ({ ...x, sku: event.target.value }))} /></label>
            <label className="auth-field">Sale price<input required type="number" min="0" step="0.01" value={itemForm.listPrice} onChange={event => setItemForm(x => ({ ...x, listPrice: event.target.value }))} /></label>
            {itemForm.kind === "PRODUCT" ? <>
              <label className="auth-field">Category<select required value={itemForm.productCategoryId} onChange={event => setItemForm(x => ({ ...x, productCategoryId: event.target.value }))}><option value="">Select a category</option>{categories.filter(x => x.isActive).map(category => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label>
              <label className="auth-field">Stock tracking<select value={itemForm.inventoryTrackingMode} onChange={event => setItemForm(x => ({ ...x, inventoryTrackingMode: event.target.value }))}><option value="QUANTITY">Quantity</option><option value="SERIALIZED_UNIT">Serialized unit</option></select></label>
            </> : <label className="auth-field catalogue-full-field">Description<textarea required maxLength={2000} value={itemForm.description} onChange={event => setItemForm(x => ({ ...x, description: event.target.value }))} /></label>}
          </div>
          <p className="form-help">SKU is optional. If supplied, it must follow the format SKU-AB12CD34 and be unique within the Company.</p>
          <div className="catalogue-form-actions"><button>{editingId ? "Save item" : "Create item"}</button>{editingId && <button type="button" className="secondary" onClick={resetForm}>Cancel edit</button>}</div>
        </form>
        <div className="catalogue-results"><h2>Products and Services</h2>{items.length === 0 ? <p className="form-help">No catalogue items found.</p> : items.map(item => <article className="catalogue-card" key={item.id}>
          <div><strong>{item.name}</strong><span>{item.kind} · {formatPrice(item.listPrice)}</span><span>{item.sku || "No SKU"}{item.kind === "PRODUCT" && item.productCategoryId ? ` · ${categories.find(x => x.id === item.productCategoryId)?.name ?? "Category"}` : ""}</span>{!item.isActive && <em>Inactive</em>}</div>
          <div className="catalogue-card-actions"><button type="button" className="secondary" onClick={() => editItem(item)}>Edit</button><button type="button" className="secondary" onClick={() => requestStatus("CatalogItem", item.id, !item.isActive)}>{item.isActive ? "Deactivate" : "Reactivate"}</button></div>
        </article>)}</div>
      </>}

      {page === "product-categories" && <>
        <form className="catalogue-form" onSubmit={saveCategory}>
          <h2>{editingId ? "Edit category" : "Create category"}</h2>
          <div className="employee-form-grid">
            <label className="auth-field">Name<input required maxLength={160} value={categoryForm.name} onChange={event => setCategoryForm(x => ({ ...x, name: event.target.value }))} /></label>
            <label className="auth-field">Code<input required maxLength={40} value={categoryForm.code} onChange={event => setCategoryForm(x => ({ ...x, code: event.target.value }))} /></label>
            <label className="auth-field">Parent category<select value={categoryForm.parentCategoryId} onChange={event => setCategoryForm(x => ({ ...x, parentCategoryId: event.target.value }))}><option value="">No parent</option>{categories.filter(x => x.isActive && x.id !== editingId).map(category => <option key={category.id} value={category.id}>{category.name}</option>)}</select></label>
            <label className="auth-field">Description (optional)<input maxLength={2000} value={categoryForm.description} onChange={event => setCategoryForm(x => ({ ...x, description: event.target.value }))} /></label>
          </div>
          <div className="catalogue-form-actions"><button>{editingId ? "Save category" : "Create category"}</button>{editingId && <button type="button" className="secondary" onClick={resetForm}>Cancel edit</button>}</div>
        </form>
        <div className="catalogue-results"><h2>Categories</h2>{categories.length === 0 ? <p className="form-help">No categories found.</p> : categories.map(category => <article className="catalogue-card" key={category.id}>
          <div><strong>{category.name}</strong><span>{category.code}{category.parentCategoryName ? ` · ${category.parentCategoryName}` : ""}</span>{category.description && <span>{category.description}</span>}{!category.isActive && <em>Inactive</em>}</div>
          <div className="catalogue-card-actions"><button type="button" className="secondary" onClick={() => editCategory(category)}>Edit</button><button type="button" className="secondary" onClick={() => requestStatus("ProductCategory", category.id, !category.isActive)}>{category.isActive ? "Deactivate" : "Reactivate"}</button></div>
        </article>)}</div>
      </>}

      {page === "fault-catalogue" && <>
        <form className="catalogue-form" onSubmit={saveFault}>
          <h2>{editingId ? "Edit Fault" : "Create Fault"}</h2>
          {editingId && <p className="form-help">Fault code: {faults.find(x => x.id === editingId)?.code ?? "Generated automatically"}</p>}
          <label className="auth-field">Description<textarea required maxLength={255} value={faultDescription} onChange={event => setFaultDescription(event.target.value)} /></label>
          <p className="form-help">A unique Fault code is generated automatically. Search and select Faults by description.</p>
          <div className="catalogue-form-actions"><button>{editingId ? "Save Fault" : "Create Fault"}</button>{editingId && <button type="button" className="secondary" onClick={resetForm}>Cancel edit</button>}</div>
        </form>
        <div className="catalogue-results"><h2>Faults</h2>{faults.length === 0 ? <p className="form-help">No Faults found.</p> : faults.map(fault => <article className="catalogue-card" key={fault.id}>
          <div><strong>{fault.description}</strong><span>{fault.code}</span>{!fault.isActive && <em>Inactive</em>}</div>
          <div className="catalogue-card-actions"><button type="button" className="secondary" onClick={() => editFault(fault)}>Edit</button><button type="button" className="secondary" onClick={() => requestStatus("Fault", fault.id, !fault.isActive)}>{fault.isActive ? "Deactivate" : "Reactivate"}</button></div>
        </article>)}</div>
      </>}
    </section>
  );
}

async function requireToken(getToken: () => Promise<string | null>) {
  const token = await getToken();
  if (!token) throw new Error("Your Shop session has expired. Sign in again.");
  return token;
}

async function responseError(response: Response) {
  try {
    const problem = await response.json() as { detail?: string; code?: string };
    return new Error(problem.detail ?? problem.code ?? `Request failed (HTTP ${response.status}).`);
  } catch {
    return new Error(`Request failed (HTTP ${response.status}).`);
  }
}

function formatPrice(value: number) {
  return new Intl.NumberFormat("en-IE", { style: "currency", currency: "EUR" }).format(value);
}
