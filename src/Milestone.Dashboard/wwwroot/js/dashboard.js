const state = {
  cameras: [],
  sites: [],
  storages: [],
  recordingServers: [],
  summary: {},
  siteName: "",
  map: null,
  cluster: null,
  manageMap: null,
  manageCluster: null,
  markers: [],
  mapCenter: { latitude: 34.0522, longitude: -118.2437, zoom: 13 },
  placingCameraId: "",
  expandedCameraId: "",
  selectedCameraIds: new Set(),
  sourceLabel: "Milestone",
  inventoryView: "cameras",
  manageMode: "table",
  page: 1,
  pageSize: 100,
  managePage: 1,
  managePageSize: 100,
  storageFilter: "",
  lifecycle: {},
  highlightsOpen: true,
  ndaaHighlightsOpen: true,
  passwordRotation: {},
  passwordHighlightsOpen: true,
  firmware: {},
  firmwareHighlightsOpen: true,
  firmwareTab: "overview"
};

const refreshButton = document.getElementById("refresh-btn");
const searchInput = document.getElementById("search");
const serverFilter = document.getElementById("server-filter");
const locationFilter = document.getElementById("location-filter");
const labelFilter = document.getElementById("label-filter");
const siteFilter = document.getElementById("site-filter");
const vendorFilter = document.getElementById("vendor-filter");
const lifecycleFilter = document.getElementById("lifecycle-filter");
const ndaaFilter = document.getElementById("ndaa-filter");
const passwordFilter = document.getElementById("password-filter");
const firmwareFilter = document.getElementById("firmware-filter");
const pageSizeSelect = document.getElementById("page-size");
const exportCameras = document.getElementById("export-cameras");
const clearFilters = document.getElementById("clear-filters");
const placeCamera = document.getElementById("place-camera");
const placeHint = document.getElementById("place-hint");
const addressSearch = document.getElementById("address-search");
const addressSearchButton = document.getElementById("address-search-btn");
const csvImport = document.getElementById("csv-import");
const selectAll = document.getElementById("select-all");
const manageSiteFilter = document.getElementById("manage-site-filter");
const manageStatusFilter = document.getElementById("manage-status-filter");
const manageLabelFilter = document.getElementById("manage-label-filter");
const managePageSizeSelect = document.getElementById("manage-page-size");
const exportSites = document.getElementById("export-sites");
const manageClearFilters = document.getElementById("manage-clear-filters");

refreshButton.addEventListener("click", () => loadDashboard());
searchInput.addEventListener("input", () => resetPageAndRender());
serverFilter.addEventListener("change", () => {
  state.inventoryView = "cameras";
  state.page = 1;
  renderInventory();
});
locationFilter.addEventListener("change", () => resetPageAndRender());
labelFilter.addEventListener("change", () => resetPageAndRender());
siteFilter.addEventListener("change", () => resetPageAndRender());
vendorFilter.addEventListener("change", () => resetPageAndRender());
lifecycleFilter.addEventListener("change", () => resetPageAndRender());
ndaaFilter.addEventListener("change", () => resetPageAndRender());
passwordFilter.addEventListener("change", () => resetPageAndRender());
firmwareFilter.addEventListener("change", () => resetPageAndRender());
pageSizeSelect.addEventListener("change", () => {
  state.pageSize = Number(pageSizeSelect.value) || 100;
  state.page = 1;
  renderCameras();
});
exportCameras.addEventListener("click", downloadCameraCsv);
clearFilters.addEventListener("click", () => {
  searchInput.value = "";
  serverFilter.value = "";
  labelFilter.value = "";
  locationFilter.value = "";
  siteFilter.value = "";
  vendorFilter.value = "";
  lifecycleFilter.value = "";
  ndaaFilter.value = "";
  passwordFilter.value = "";
  firmwareFilter.value = "";
  state.page = 1;
  renderInventory();
});
placeCamera.addEventListener("change", () => {
  state.placingCameraId = placeCamera.value;
  updatePlaceHint();
});
addressSearchButton.addEventListener("click", searchAddress);
addressSearch.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    event.preventDefault();
    searchAddress();
  }
});
csvImport.addEventListener("change", importCsv);
manageSiteFilter.addEventListener("change", () => {
  state.managePage = 1;
  renderSites();
});
manageStatusFilter.addEventListener("change", () => {
  state.managePage = 1;
  renderSites();
});
manageLabelFilter.addEventListener("change", () => {
  state.managePage = 1;
  renderSites();
});
managePageSizeSelect.addEventListener("change", () => {
  state.managePageSize = Number(managePageSizeSelect.value) || 100;
  state.managePage = 1;
  renderSites();
});
exportSites.addEventListener("click", downloadSiteCsv);
manageClearFilters.addEventListener("click", () => {
  manageSiteFilter.value = "";
  manageStatusFilter.value = "";
  manageLabelFilter.value = "";
  state.managePage = 1;
  renderSites();
});
document.querySelectorAll("[data-manage-mode]").forEach((button) => {
  button.addEventListener("click", () => setManageMode(button.dataset.manageMode));
});
document.addEventListener("click", (event) => {
  const opener = event.target.closest("[data-open]");
  if (!opener) {
    return;
  }
  if (opener.dataset.open === "servers") {
    showRecordingServers();
  } else if (opener.dataset.open === "cameras") {
    showAllCameras();
  } else if (opener.dataset.open === "manage") {
    showView("manage");
  } else if (opener.dataset.open === "storage") {
    showStoragePies("");
  } else if (opener.dataset.open === "archives") {
    showStoragePies("Archive");
  }
});
document.addEventListener("click", (event) => {
  const life = event.target.closest("[data-life-filter], [data-life-site], [data-life-query], [data-ndaa-filter], [data-password-filter], [data-firmware-filter]");
  if (!life) {
    return;
  }
  showCamerasForLifecycle(life.dataset.lifeFilter ?? "", {
    site: life.dataset.lifeSite,
    query: life.dataset.lifeQuery,
    ndaa: life.dataset.ndaaFilter,
    password: life.dataset.passwordFilter,
    firmware: life.dataset.firmwareFilter
  });
});
document.querySelectorAll("[data-firmware-tab]").forEach((button) => {
  button.addEventListener("click", () => setFirmwareTab(button.dataset.firmwareTab));
});
document.getElementById("highlights-toggle")?.addEventListener("click", () => {
  state.highlightsOpen = !state.highlightsOpen;
  document.getElementById("lifecycle-highlights-card")?.classList.toggle("collapsed", !state.highlightsOpen);
  document.getElementById("highlights-toggle")?.setAttribute("aria-expanded", String(state.highlightsOpen));
});
document.getElementById("ndaa-highlights-toggle")?.addEventListener("click", () => {
  state.ndaaHighlightsOpen = !state.ndaaHighlightsOpen;
  document.querySelector(".ndaa-highlights-card")?.classList.toggle("collapsed", !state.ndaaHighlightsOpen);
  document.getElementById("ndaa-highlights-toggle")?.setAttribute("aria-expanded", String(state.ndaaHighlightsOpen));
});
document.getElementById("password-highlights-toggle")?.addEventListener("click", () => {
  state.passwordHighlightsOpen = !state.passwordHighlightsOpen;
  document.getElementById("password-highlights-card")?.classList.toggle("collapsed", !state.passwordHighlightsOpen);
  document.getElementById("password-highlights-toggle")?.setAttribute("aria-expanded", String(state.passwordHighlightsOpen));
});
document.getElementById("firmware-highlights-toggle")?.addEventListener("click", () => {
  state.firmwareHighlightsOpen = !state.firmwareHighlightsOpen;
  document.getElementById("firmware-highlights-card")?.classList.toggle("collapsed", !state.firmwareHighlightsOpen);
  document.getElementById("firmware-highlights-toggle")?.setAttribute("aria-expanded", String(state.firmwareHighlightsOpen));
});
selectAll.addEventListener("change", () => {
  const { pageRows } = pagedCameras();
  if (selectAll.checked) {
    pageRows.forEach((camera) => state.selectedCameraIds.add(camera.id));
  } else {
    pageRows.forEach((camera) => state.selectedCameraIds.delete(camera.id));
  }
  renderCameras();
});

document.querySelectorAll(".nav-btn").forEach((button) => {
  button.addEventListener("click", () => {
    showView(button.dataset.view, { focus: button.dataset.focus });
  });
});

function showView(name, options = {}) {
  document.querySelectorAll(".view").forEach((view) => {
    view.hidden = view.id !== `view-${name}`;
  });
  document.querySelectorAll(".nav-btn").forEach((item) => {
    const mapFocus = name === "dashboard" && options.focus === "sites-view";
    item.classList.toggle("active", mapFocus
      ? item.dataset.focus === "sites-view"
      : item.dataset.view === name && !item.dataset.focus);
  });
  if (name === "dashboard") {
    setTimeout(() => state.map?.invalidateSize(), 80);
  }
  if (name === "manage") {
    setTimeout(() => {
      renderSites();
      state.manageMap?.invalidateSize();
    }, 80);
  }
  if (name === "storage" && !options.focus) {
    state.storageFilter = "";
    renderStoragePies();
  }
  if (name === "lifecycle") {
    renderLifecycle();
  }
  if (name === "passwords") {
    renderPasswords();
  }
  if (name === "firmware") {
    renderFirmware();
  }
  if (options.focus) {
    document.getElementById(options.focus)?.scrollIntoView({ behavior: "smooth", block: "start" });
    return;
  }
  window.scrollTo({ top: 0, behavior: "smooth" });
}

async function loadDashboard() {
  refreshButton.disabled = true;
  const pageError = document.getElementById("page-error");
  pageError.hidden = true;
  try {
    const response = await fetch("/api/dashboard", { cache: "no-store" });
    if (!response.ok) {
      let detail = `Dashboard API returned ${response.status}`;
      try {
        const payload = await response.json();
        if (payload.error) {
          detail = payload.error;
        }
      } catch {
        // Keep the status text when the body is not JSON.
      }
      throw new Error(detail);
    }

    const data = await response.json();
    state.cameras = data.cameras ?? [];
    state.sites = data.sites ?? [];
    state.storages = data.storages ?? [];
    state.recordingServers = data.recordingServers ?? [];
    state.summary = data.summary ?? {};
    state.lifecycle = data.lifecycle ?? {};
    state.passwordRotation = data.passwordRotation ?? {};
    state.firmware = data.firmware ?? {};
    state.siteName = data.siteName ?? "";
    state.mapCenter = data.mapCenter ?? state.mapCenter;
    renderSource(data);
    renderOverview();
    renderDeviceTypes();
    renderOperational();
    renderDonut();
    renderMaintenance();
    renderStorage(state.storages);
    renderStoragePies();
    fillServerFilter(state.recordingServers);
    fillLabelFilter(state.cameras);
    fillChoiceFilter(siteFilter, state.cameras.map((camera) => camera.site), "Site");
    fillChoiceFilter(vendorFilter, state.cameras.map((camera) => camera.vendor), "Vendor");
    fillPlaceCamera();
    fillManageFilters();
    renderInventory();
    renderSites();
    renderLifecycle();
    renderPasswords();
    renderFirmware();
    try {
      renderMap(state.cameras);
    } catch (mapError) {
      document.getElementById("map-copy").textContent = `Map could not start: ${mapError.message}`;
    }
  } catch (error) {
    document.getElementById("source-badge").textContent = "Unavailable";
    pageError.hidden = false;
    pageError.textContent = `Could not load dashboard: ${error.message}`;
  } finally {
    refreshButton.disabled = false;
  }
}

function renderSource(data) {
  const badge = document.getElementById("source-badge");
  const source = data.source ?? "unknown";
  badge.textContent = source === "demo" ? "Demo data" : "Live";
  badge.className = `badge ${source === "demo" ? "demo" : "live"}`;
  state.sourceLabel = source === "demo" ? "Demo" : "Milestone Production";
  document.getElementById("generated-at").textContent = data.generatedAt
    ? new Date(data.generatedAt).toLocaleString()
    : "";
}

function counts() {
  const cameras = state.cameras;
  const storages = state.storages;
  const servers = state.recordingServers;
  return {
    cameras: cameras.length,
    enabled: cameras.filter((item) => item.enabled).length,
    disabled: cameras.filter((item) => !item.enabled).length,
    mapped: cameras.filter((item) => item.location).length,
    unmapped: cameras.filter((item) => !item.location).length,
    servers: servers.length,
    serversOnline: servers.filter((item) => item.enabled !== false).length,
    storage: storages.length,
    archives: storages.filter((item) => item.kind === "Archive").length,
    warn: storages.filter((item) => item.usagePercent >= 75).length,
    critical: storages.filter((item) => item.usagePercent >= 90).length,
    unavailable: storages.filter((item) => item.isAvailable === false || item.isMounted === false).length,
    locked: storages.filter((item) => item.lockedUsedSpaceMb > 0).length
  };
}

function renderOverview() {
  const data = counts();
  const siteLabel = state.siteName || "XProtect site";
  document.getElementById("overview").innerHTML = `
    <article class="summary-card clickable" data-open="manage" title="Open Sites View">
      <h3>Managed sites</h3>
      <div class="value">${state.sites.length || 1}</div>
      <div class="legend">
        <span><i class="dot teal"></i>${escapeHtml(siteLabel)}</span>
        <span><i class="dot orange"></i>${data.unmapped} cameras unmapped</span>
      </div>
      <div class="card-action">View all ${state.sites.length || 1} sites →</div>
    </article>
    <article class="summary-card clickable" data-open="cameras" title="Show all cameras">
      <h3>Managed cameras</h3>
      <div class="value">${formatCount(data.cameras)}</div>
      <div class="legend">
        <span><i class="dot teal"></i>${data.enabled} Enabled</span>
        <span><i class="dot red"></i>${data.disabled} Disabled</span>
        <span><i class="dot orange"></i>${data.unmapped} Unmapped</span>
      </div>
    </article>
    <article class="summary-card clickable ${state.inventoryView === "servers" ? "selected" : ""}" data-open="servers" title="Show all recording servers">
      <h3>Recording servers</h3>
      <div class="value">${data.servers}</div>
      <div class="legend">
        <span><i class="dot teal"></i>${data.serversOnline} Online</span>
        <span><i class="dot gray"></i>${data.storage} storage volumes</span>
      </div>
      <div class="card-action">View all ${data.servers} servers →</div>
    </article>
  `;
}

function renderDeviceTypes() {
  const data = counts();
  const items = [
    [data.cameras, "Camera"],
    [data.servers, "Recording Server"],
    [data.storage, "Storage"],
    [data.archives, "Archive"],
    [data.mapped, "Mapped"],
    [data.unmapped, "Unmapped"]
  ];
  document.getElementById("device-types").innerHTML = items.map(([count, label]) => {
    const open = label === "Recording Server" ? "servers"
      : label === "Camera" ? "cameras"
      : label === "Storage" ? "storage"
      : label === "Archive" ? "archives"
      : "";
    return `
    <div class="device-item ${open ? "clickable" : ""}" ${open ? `data-open="${open}"` : ""} title="${open ? `Open ${label} details` : ""}">
      <div class="count">${formatCount(count)}</div>
      <div class="label">${label}</div>
    </div>
  `;
  }).join("");
}

function matchesLifecycleFilter(camera, filter) {
  const status = camera.intelligence?.lifecycleStatus ?? "";
  if (!filter) {
    return true;
  }
  if (filter === "compliant") {
    return status === "Active" || status === "EOL";
  }
  if (filter === "noncompliant") {
    return status === "EOS";
  }
  if (filter === "na") {
    return !status;
  }
  return status === filter;
}

function lifecycleLabel(filter) {
  return {
    compliant: "Compliant",
    noncompliant: "Non Compliant",
    Active: "Current Product",
    EOL: "EOL",
    EOS: "EOS",
    na: "N/A"
  }[filter] ?? filter;
}

function matchesNdaaFilter(camera, filter) {
  const status = camera.intelligence?.ndaaStatus ?? "";
  if (!filter) {
    return true;
  }
  if (filter === "unknown") {
    return !status;
  }
  return status === filter;
}

function ndaaLabel(filter) {
  return filter === "unknown" ? "Unknown" : filter;
}

function matchesPasswordFilter(camera, filter) {
  const status = camera.intelligence?.passwordExpiryStatus ?? "";
  if (!filter) {
    return true;
  }
  if (filter === "compliant") {
    return status === "Up To Date" || status === "Due Soon";
  }
  if (filter === "noncompliant") {
    return status === "Never Rotated" || status === "Overdue";
  }
  if (filter === "na") {
    return !status;
  }
  return status === filter;
}

function passwordLabel(filter) {
  return {
    compliant: "Password Compliant",
    noncompliant: "Password Non Compliant",
    "Up To Date": "Up To Date",
    "Never Rotated": "Never Rotated",
    Overdue: "Expired",
    "Due Soon": "Soon To Be Expired",
    na: "Password N/A"
  }[filter] ?? filter;
}

function renderPasswords() {
  const pw = state.passwordRotation ?? {};
  const total = pw.totalDevices ?? 0;
  const compliant = pw.compliantCount ?? 0;
  const nonCompliant = pw.nonCompliantCount ?? 0;
  const na = pw.naCount ?? 0;
  const percent = pw.overallCompliancePercent ?? 0;
  document.getElementById("password-summary").innerHTML = `
    <article class="summary-card life-card">
      <h3>DEVICES</h3>
      <div class="pw-devices">
        <div class="pw-gauge" style="background:${donutGradient(
          [{ count: percent }, { count: Math.max(100 - percent, 0) }],
          ["#27ae60", "#e55353"]
        )}">
          <div class="pw-gauge-inner">
            <b>${percent}%</b>
            <span class="muted">Overall Compliance</span>
          </div>
        </div>
        <div class="life-legend">
          <button type="button" data-password-filter="compliant"><i class="dot green"></i>Compliant: ${formatInt(compliant)}</button>
          <button type="button" data-password-filter="noncompliant"><i class="dot red"></i>Non-Compliant: ${formatInt(nonCompliant)}</button>
          <button type="button" data-password-filter="na"><i class="dot gray"></i>N/A: ${formatInt(na)}</button>
          <div class="total">Total Devices: ${formatInt(total)}</div>
        </div>
      </div>
    </article>
    <article class="summary-card life-card">
      <h3>PASSWORD EXPIRATION STATUS</h3>
      <div class="life-status">
        <button type="button" class="life-badge fresh" data-password-filter="Up To Date">
          <span>UP TO DATE</span>
          <strong>${formatInt(pw.upToDateCount)}</strong>
        </button>
        <button type="button" class="life-badge never" data-password-filter="Never Rotated">
          <span>NEVER ROTATED</span>
          <strong>${formatInt(pw.neverRotatedCount)}</strong>
        </button>
        <button type="button" class="life-badge eos" data-password-filter="Overdue">
          <span>EXPIRED</span>
          <strong>${formatInt(pw.expiredCount)}</strong>
        </button>
        <button type="button" class="life-badge na" data-password-filter="na">
          <span>N/A</span>
          <strong>${formatInt(pw.naCount)}</strong>
        </button>
        <button type="button" class="life-badge soon" data-password-filter="Due Soon">
          <span>SOON TO BE EXPIRED</span>
          <strong>${formatInt(pw.soonCount)}</strong>
        </button>
      </div>
    </article>
    <article class="summary-card life-card">
      <h3>SITES</h3>
      <div class="life-sites">
        <button type="button" data-password-filter="compliant"><i class="dot green"></i>Compliant: ${formatInt(pw.compliantSites)}</button>
        <button type="button" data-password-filter="noncompliant"><i class="dot red"></i>Non-Compliant: ${formatInt(pw.nonCompliantSites)}</button>
        <div class="total">Total Sites: ${formatInt(pw.totalSites)}</div>
      </div>
    </article>
  `;

  document.getElementById("password-highlights").innerHTML = `
    ${renderPasswordAlertedSites(pw.topAlertedSites ?? [])}
    ${renderPasswordDonut("Non-Compliant By User Type", pw.nonCompliantByUserType ?? [], ["#1d4e89", "#3d8bfd", "#7eb8da", "#5aa9e6"], "noncompliant", false)}
    ${renderPasswordDonut("Non-Compliant By Device Type", pw.nonCompliantByDeviceType ?? [], ["#3d8bfd", "#2b6cb0", "#7eb8da", "#1d4e89"], "noncompliant", true)}
  `;
  document.getElementById("password-breakdown").innerHTML = renderPasswordBreakdown(pw.expirationBreakdown ?? []);
}

function renderPasswordAlertedSites(rows) {
  const body = rows.length === 0
    ? `<tr><td colspan="4" class="muted">No password alerts were found.</td></tr>`
    : rows.map((row) => `
      <tr>
        <td><button class="link-btn" type="button" data-life-site="${escapeHtml(row.site)}" data-password-filter="noncompliant">${escapeHtml(row.site)}</button></td>
        <td>${formatInt(row.alerted)}</td>
        <td>${formatInt(row.total)}</td>
        <td>
          <div class="risk-cell">
            <div class="risk-bar"><span style="width:${Math.min(row.riskPercent ?? 0, 100)}%"></span></div>
            ${Number(row.riskPercent ?? 0).toFixed(1)}%
          </div>
        </td>
      </tr>
    `).join("");
  return `
    <article class="highlight-card">
      <h3>Top Alerted Sites</h3>
      <table class="alert-table">
        <thead>
          <tr><th>Site</th><th>Alerted Devices</th><th>Total</th><th>Risk Level</th></tr>
        </thead>
        <tbody>${body}</tbody>
      </table>
    </article>
  `;
}

function renderPasswordDonut(title, slices, colors, filter, useQuery = false) {
  const total = slices.reduce((sum, slice) => sum + (slice.count ?? 0), 0);
  return `
    <article class="highlight-card">
      <h3>${escapeHtml(title)}</h3>
      ${total === 0 ? `<p class="muted">No devices in this category.</p>` : `
      <div class="life-donut-wrap">
        <div class="life-donut-legend">
          ${slices.map((slice, index) => `
            <button type="button" class="ndaa-legend-row" data-password-filter="${filter}" ${useQuery ? `data-life-query="${escapeHtml(slice.label)}"` : ""}>
              <span><i class="dot" style="background:${colors[index % colors.length]}"></i>${escapeHtml(slice.label)}</span>
              <strong>${formatInt(slice.count)}</strong>
            </button>
          `).join("")}
        </div>
        <div class="life-donut" style="background:${donutGradient(slices, colors)}">
          <div class="life-donut-inner">${formatInt(total)}</div>
        </div>
      </div>`}
    </article>
  `;
}

function renderPasswordBreakdown(rows) {
  const max = Math.max(...rows.map((row) => row.count ?? 0), 1);
  if (rows.length === 0) {
    return `<p class="muted">No password expiry dates were reported.</p>`;
  }
  return `
    <div class="pw-breakdown">
      ${rows.map((row) => `
        <div class="year-col">
          <div class="year-count">${formatInt(row.count)}</div>
          <div class="year-bar" style="height:${Math.max(((row.count ?? 0) / max) * 160, 6)}px"></div>
          <div class="year-label">${escapeHtml(row.label)}</div>
        </div>
      `).join("")}
    </div>
  `;
}

function hasNoFirmware(camera) {
  return !camera.firmware;
}

function hasFirmwareUpgrade(camera) {
  const firmware = camera.firmware ?? "";
  const suggested = camera.intelligence?.suggestedFirmware ?? "";
  return Boolean(firmware && suggested && !firmware.toLowerCase().startsWith(suggested.toLowerCase()));
}

function isFirmwareOutdated(camera) {
  return camera.intelligence?.lifecycleStatus === "EOS" || hasFirmwareUpgrade(camera);
}

function isFirmwareVulnerable(camera) {
  return camera.intelligence?.vulnerabilitySeverity === "High"
    || camera.intelligence?.vulnerabilitySeverity === "Medium";
}

function matchesFirmwareFilter(camera, filter) {
  if (!filter) {
    return true;
  }
  if (filter === "na") {
    return hasNoFirmware(camera);
  }
  if (filter === "compliant") {
    return !hasNoFirmware(camera) && !isFirmwareOutdated(camera);
  }
  if (filter === "noncompliant") {
    return isFirmwareOutdated(camera);
  }
  if (filter === "vulnerable") {
    return isFirmwareVulnerable(camera);
  }
  if (filter === "upgrade") {
    return hasFirmwareUpgrade(camera);
  }
  return true;
}

function firmwareLabel(filter) {
  return {
    compliant: "Compliant version",
    noncompliant: "Outdated firmware",
    vulnerable: "Vulnerable firmware",
    upgrade: "Available upgrade",
    na: "Firmware N/A"
  }[filter] ?? filter;
}

function setFirmwareTab(tab) {
  state.firmwareTab = tab === "details" ? "details" : "overview";
  document.getElementById("firmware-overview-panel").hidden = state.firmwareTab !== "overview";
  document.getElementById("firmware-details-panel").hidden = state.firmwareTab !== "details";
  document.getElementById("firmware-tab-overview")?.classList.toggle("active", state.firmwareTab === "overview");
  document.getElementById("firmware-tab-overview")?.classList.toggle("life-tab", state.firmwareTab === "overview");
  document.getElementById("firmware-tab-details")?.classList.toggle("active", state.firmwareTab === "details");
  document.getElementById("firmware-tab-details")?.classList.toggle("life-tab", state.firmwareTab === "details");
}

function renderFirmware() {
  const fw = state.firmware ?? {};
  const total = fw.totalDevices ?? 0;
  const compliant = fw.compliantCount ?? 0;
  const nonCompliant = fw.nonCompliantCount ?? 0;
  const na = fw.naCount ?? 0;
  const percent = fw.overallCompliancePercent ?? 0;
  const scored = compliant + nonCompliant;
  const okWidth = scored ? (compliant / scored) * 100 : 0;
  const badWidth = scored ? (nonCompliant / scored) * 100 : 0;
  document.getElementById("firmware-summary").innerHTML = `
    <article class="summary-card life-card">
      <h3>DEVICES</h3>
      <div class="life-devices">
        <div class="life-score">
          <div class="value">${percent}%</div>
          <div class="muted">Overall Compliance</div>
          <div class="life-bar" title="${percent}% compliant">
            <span class="ok" style="width:${okWidth}%;background:#27ae60"></span>
            <span class="bad" style="width:${badWidth}%"></span>
          </div>
        </div>
        <div class="life-legend">
          <button type="button" data-firmware-filter="compliant"><i class="dot green"></i>Compliant: ${formatInt(compliant)}</button>
          <button type="button" data-firmware-filter="noncompliant"><i class="dot red"></i>Non Compliant: ${formatInt(nonCompliant)}</button>
          <button type="button" data-firmware-filter="na"><i class="dot gray"></i>N/A: ${formatInt(na)}</button>
          <div class="total">Total Devices: ${formatInt(total)}</div>
        </div>
      </div>
    </article>
    <article class="summary-card life-card">
      <h3>OUTDATED FIRMWARE STATUS</h3>
      <div class="life-status">
        <button type="button" class="life-badge fresh" data-firmware-filter="compliant">
          <span>COMPLIANT VERSION</span>
          <strong>${formatInt(fw.compliantVersionCount)}</strong>
        </button>
        <button type="button" class="life-badge vuln" data-firmware-filter="vulnerable">
          <span>VULNERABLE FIRMWARE</span>
          <strong>${formatInt(fw.vulnerableCount)}</strong>
        </button>
        <button type="button" class="life-badge upgrade" data-firmware-filter="upgrade">
          <span>AVAILABLE UPGRADE</span>
          <strong>${formatInt(fw.availableUpgradeCount)}</strong>
        </button>
        <button type="button" class="life-badge na" data-firmware-filter="na">
          <span>N/A</span>
          <strong>${formatInt(fw.naCount)}</strong>
        </button>
      </div>
    </article>
    <article class="summary-card life-card">
      <h3>SITES</h3>
      <div class="life-sites">
        <button type="button" data-firmware-filter="compliant"><i class="dot green"></i>Compliant: ${formatInt(fw.compliantSites)}</button>
        <button type="button" data-firmware-filter="noncompliant"><i class="dot red"></i>Non Compliant: ${formatInt(fw.nonCompliantSites)}</button>
        <div class="total">Total Sites: ${formatInt(fw.totalSites)}</div>
      </div>
    </article>
  `;
  document.getElementById("firmware-highlights").innerHTML = `
    ${renderFirmwareAlertedSites(fw.topAlertedSites ?? [])}
    ${renderPasswordDonut("Top Non Compliant Vendor & Model", fw.topNonCompliantModels ?? [], ["#1d4e89", "#3d8bfd", "#7eb8da", "#5aa9e6", "#2b6cb0"], "noncompliant", true)}
    ${renderPasswordDonut("Top Non Compliant by Device Type", fw.topNonCompliantTypes ?? [], ["#3d8bfd", "#2b6cb0", "#7eb8da", "#1d4e89"], "noncompliant", true)}
  `.replaceAll("data-password-filter", "data-firmware-filter");
  renderFirmwareDetails(fw.details ?? []);
  setFirmwareTab(state.firmwareTab);
}

function renderFirmwareAlertedSites(rows) {
  const body = rows.length === 0
    ? `<tr><td colspan="4" class="muted">No outdated firmware was found.</td></tr>`
    : rows.map((row) => {
      const filled = Math.round((row.riskPercent ?? 0) / 20);
      return `
      <tr>
        <td><button class="link-btn" type="button" data-life-site="${escapeHtml(row.site)}" data-firmware-filter="noncompliant">${escapeHtml(row.site)}</button></td>
        <td>${formatInt(row.alerted)}</td>
        <td>${formatInt(row.total)}</td>
        <td>
          <div class="risk-cell">
            <div class="risk-segments">${[0, 1, 2, 3, 4].map((index) => `<i class="${index < filled ? "on" : ""}"></i>`).join("")}</div>
            ${Number(row.riskPercent ?? 0).toFixed(1)}%
          </div>
        </td>
      </tr>`;
    }).join("");
  return `
    <article class="highlight-card">
      <h3>Top Alerted Sites</h3>
      <table class="alert-table">
        <thead>
          <tr><th>Site</th><th>Alerted Devices</th><th>Total</th><th>Risk Level</th></tr>
        </thead>
        <tbody>${body}</tbody>
      </table>
    </article>
  `;
}

function renderFirmwareDetails(rows) {
  document.getElementById("firmware-detail-body").innerHTML = rows.map((row) => `
    <tr>
      <td>${escapeHtml(row.name)}</td>
      <td>${escapeHtml(row.site ?? "Unassigned")}</td>
      <td>${escapeHtml(row.vendor ?? "—")}</td>
      <td>${escapeHtml(row.model ?? "—")}</td>
      <td>${escapeHtml(row.firmware ?? "—")}</td>
      <td>${escapeHtml(row.suggestedFirmware ?? "—")}</td>
      <td>${escapeHtml(row.status ?? "—")}</td>
      <td>${severityCell(row.vulnerability)}</td>
    </tr>
  `).join("") || `<tr><td colspan="8" class="muted">No outdated or vulnerable firmware was found.</td></tr>`;
}

function renderLifecycle() {
  const life = state.lifecycle ?? {};
  const total = life.totalDevices ?? 0;
  const compliant = life.compliantCount ?? 0;
  const nonCompliant = life.nonCompliantCount ?? 0;
  const na = life.naCount ?? 0;
  const percent = life.overallCompliancePercent ?? 0;
  const scored = compliant + nonCompliant;
  const okWidth = scored ? (compliant / scored) * 100 : 0;
  const badWidth = scored ? (nonCompliant / scored) * 100 : 0;

  document.getElementById("lifecycle-summary").innerHTML = `
    <article class="summary-card life-card">
      <h3>DEVICES</h3>
      <div class="life-devices">
        <div class="life-score">
          <div class="value">${percent}%</div>
          <div class="muted">Overall Compliance</div>
          <div class="life-bar" title="${percent}% compliant">
            <span class="ok" style="width:${okWidth}%"></span>
            <span class="bad" style="width:${badWidth}%"></span>
          </div>
        </div>
        <div class="life-legend">
          <button type="button" data-life-filter="compliant"><i class="dot teal"></i>Compliant: ${formatInt(compliant)}</button>
          <button type="button" data-life-filter="noncompliant"><i class="dot pink"></i>Non Compliant: ${formatInt(nonCompliant)}</button>
          <button type="button" data-life-filter="na"><i class="dot gray"></i>N/A: ${formatInt(na)}</button>
          <div class="total">Total Devices: ${formatInt(total)}</div>
        </div>
      </div>
    </article>
    <article class="summary-card life-card">
      <h3>DEVICE LIFECYCLE STATUS</h3>
      <div class="life-status">
        <button type="button" class="life-badge current" data-life-filter="Active">
          <span>CURRENT PRODUCT</span>
          <strong>${formatInt(life.currentProductCount)}</strong>
        </button>
        <button type="button" class="life-badge eol" data-life-filter="EOL">
          <span>EOL</span>
          <strong>${formatInt(life.eolCount)}</strong>
        </button>
        <button type="button" class="life-badge eos" data-life-filter="EOS">
          <span>EOS</span>
          <strong>${formatInt(life.eosCount)}</strong>
        </button>
        <button type="button" class="life-badge na" data-life-filter="na">
          <span>N/A</span>
          <strong>${formatInt(life.naCount)}</strong>
        </button>
      </div>
    </article>
    <article class="summary-card life-card">
      <h3>SITES</h3>
      <div class="life-sites">
        <button type="button" data-life-filter="compliant"><i class="dot teal"></i>Compliant: ${formatInt(life.compliantSites)}</button>
        <button type="button" data-life-filter="noncompliant"><i class="dot pink"></i>Non Compliant: ${formatInt(life.nonCompliantSites)}</button>
        <div class="total">Total Sites: ${formatInt(life.totalSites)}</div>
      </div>
    </article>
  `;

  document.getElementById("lifecycle-highlights").innerHTML = `
    ${renderAlertedSites(life.topAlertedSites ?? [])}
    ${renderLifeDonut("Top Non-Compliant Vendor & Model", life.topNonCompliantModels ?? [], ["#1aae9f", "#148f83", "#5ecfc2", "#0e6e65", "#9adfd6", "#3d8bfd"], "EOS")}
    ${renderLifeDonut("Top Non-Compliant by Type", life.topNonCompliantTypes ?? [], ["#3d8bfd", "#2b6cb0", "#7eb8da", "#1d4e89", "#a8c8ea", "#5aa9e6"], "EOS")}
    ${renderEosYears(life.eosByYear ?? [])}
    ${renderLifeDonut("Top EOL by Vendor & Model", life.topEolModels ?? [], ["#8ec5ff", "#5aa9e6", "#3d8bfd", "#b9d9f7", "#2b6cb0", "#7eb8da"], "EOL")}
    ${renderLifeDonut("EOL by Type", life.eolByType ?? [], ["#1d4e89", "#2b6cb0", "#3d8bfd", "#5aa9e6", "#7eb8da", "#8ec5ff"], "EOL")}
  `;
  document.getElementById("ndaa-highlights").innerHTML = renderNdaaStatus(life);
}

function renderNdaaStatus(life) {
  const slices = [
    { label: "Compliant", count: life.ndaaCompliantCount ?? 0, color: "#1d4e89", filter: "Compliant" },
    { label: "Restricted", count: life.ndaaRestrictedCount ?? 0, color: "#e5537a", filter: "Restricted" },
    { label: "Unknown", count: life.ndaaUnknownCount ?? 0, color: "#5aa9e6", filter: "unknown" }
  ];
  const total = slices.reduce((sum, slice) => sum + slice.count, 0);
  return `
    <article class="highlight-card">
      <h3>NDAA Status</h3>
      ${total === 0 ? `<p class="muted">No devices were returned.</p>` : `
      <div class="life-donut-wrap">
        <div class="life-donut-legend">
          ${slices.map((slice) => `
            <button type="button" class="ndaa-legend-row" data-ndaa-filter="${slice.filter}">
              <span><i class="dot" style="background:${slice.color}"></i>${escapeHtml(slice.label)}</span>
              <strong>${formatInt(slice.count)}</strong>
            </button>
          `).join("")}
        </div>
        <div class="life-donut" style="background:${donutGradient(slices, slices.map((slice) => slice.color))}">
          <div class="life-donut-inner">${formatInt(total)}</div>
        </div>
      </div>`}
    </article>
  `;
}

function renderAlertedSites(rows) {
  const body = rows.length === 0
    ? `<tr><td colspan="5" class="muted">No EOS or EOL devices were found.</td></tr>`
    : rows.map((row) => `
      <tr>
        <td><button class="link-btn" type="button" data-life-site="${escapeHtml(row.site)}">${escapeHtml(row.site)}</button></td>
        <td>${formatInt(row.eos)}</td>
        <td>${formatInt(row.eol)}</td>
        <td>${formatInt(row.total)}</td>
        <td>
          <div class="risk-cell">
            <div class="risk-bar"><span style="width:${Math.min(row.riskPercent ?? 0, 100)}%"></span></div>
            ${Number(row.riskPercent ?? 0).toFixed(1)}%
          </div>
        </td>
      </tr>
    `).join("");
  return `
    <article class="highlight-card">
      <h3>Top Alerted Sites</h3>
      <table class="alert-table">
        <thead>
          <tr><th>Site</th><th>EOS</th><th>EOL</th><th>Total</th><th>Risk Level</th></tr>
        </thead>
        <tbody>${body}</tbody>
      </table>
    </article>
  `;
}

function renderLifeDonut(title, slices, colors, filter) {
  const total = slices.reduce((sum, slice) => sum + (slice.count ?? 0), 0);
  return `
    <article class="highlight-card">
      <h3>${escapeHtml(title)}</h3>
      ${total === 0 ? `<p class="muted">No devices in this category.</p>` : `
      <div class="life-donut-wrap">
        <div class="life-donut-legend">
          ${slices.map((slice, index) => `
            <button type="button" data-life-filter="${filter}" data-life-query="${escapeHtml(slice.label)}">
              <i class="dot" style="background:${colors[index % colors.length]}"></i>
              ${escapeHtml(slice.label)}: ${formatInt(slice.count)}
            </button>
          `).join("")}
        </div>
        <div class="life-donut" style="background:${donutGradient(slices, colors)}">
          <div class="life-donut-inner">${formatInt(total)}</div>
        </div>
      </div>`}
    </article>
  `;
}

function renderEosYears(rows) {
  const max = Math.max(...rows.map((row) => row.count ?? 0), 1);
  return `
    <article class="highlight-card">
      <h3>EOS by Year</h3>
      ${rows.length === 0 ? `<p class="muted">No published EOS dates.</p>` : `
      <div class="year-chart">
        ${rows.map((row) => `
          <div class="year-col">
            <div class="year-count">${formatInt(row.count)}</div>
            <div class="year-bar" style="height:${Math.max(((row.count ?? 0) / max) * 150, 6)}px"></div>
            <div class="year-label">${row.year}</div>
          </div>
        `).join("")}
      </div>`}
    </article>
  `;
}

function donutGradient(slices, colors) {
  const total = slices.reduce((sum, slice) => sum + (slice.count ?? 0), 0) || 1;
  let start = 0;
  const stops = slices.map((slice, index) => {
    const end = start + ((slice.count ?? 0) / total) * 100;
    const stop = `${colors[index % colors.length]} ${start}% ${end}%`;
    start = end;
    return stop;
  });
  return `conic-gradient(${stops.join(", ")})`;
}

function formatInt(value) {
  return Number(value ?? 0).toLocaleString();
}

function renderOperational() {
  const data = counts();
  const rows = [
    ["Unmapped cameras", data.unmapped, data.unmapped > 0 ? "up" : "down"],
    ["Disabled cameras", data.disabled, data.disabled > 0 ? "up" : "down"],
    ["Storage over 75%", data.warn, data.warn > 0 ? "up" : "down"],
    ["Storage over 90%", data.critical, data.critical > 0 ? "up" : "down"],
    ["Unavailable volumes", data.unavailable, data.unavailable > 0 ? "up" : "down"]
  ];
  document.getElementById("alert-list").innerHTML = rows.map(([label, value, trend]) => `
    <div class="stat-row">
      <span>${label}</span>
      <strong class="${trend}">${value} ${trend === "up" ? "▲" : "▼"}</strong>
    </div>
  `).join("");
}

function renderDonut() {
  const data = counts();
  const total = Math.max(data.cameras, 1);
  const mappedPct = (data.mapped / total) * 100;
  document.getElementById("donut").style.background =
    `conic-gradient(var(--teal) 0 ${mappedPct}%, var(--red) ${mappedPct}% 100%)`;
  document.getElementById("donut").innerHTML = `
    <div class="donut-inner">
      <b>${data.unmapped}</b>
      Unmapped cameras
      <div class="muted">${data.cameras} total cameras</div>
    </div>
  `;
}

function renderMaintenance() {
  const data = counts();
  const rows = [
    ["Locked evidence data", data.locked],
    ["Archive volumes", data.archives],
    ["Cameras still unmapped", data.unmapped],
    ["Critical storage", data.critical]
  ];
  document.getElementById("maintenance-list").innerHTML = rows.map(([label, value]) => `
    <div class="stat-row">
      <span>${label}</span>
      <strong>${value}</strong>
    </div>
  `).join("");
}

function showStoragePies(kind) {
  state.storageFilter = kind || "";
  renderStoragePies();
  showView("storage", { focus: "storage-pies" });
}

function visibleStorages() {
  if (state.storageFilter === "Archive") {
    return state.storages.filter((storage) => storage.kind === "Archive");
  }
  if (state.storageFilter === "Recording") {
    return state.storages.filter((storage) => storage.kind !== "Archive");
  }
  return state.storages;
}

function renderStoragePies() {
  const storages = visibleStorages();
  const kindLabel = state.storageFilter === "Archive" ? "archive" : "storage";
  document.getElementById("storage-pies-title").textContent =
    `${storages.length} ${kindLabel} volume${storages.length === 1 ? "" : "s"}`;
  document.getElementById("storage-pies-copy").textContent = state.storageFilter === "Archive"
    ? "One pie chart for each archive volume."
    : "One pie chart for each recording and archive volume.";
  document.getElementById("storage-pie-grid").innerHTML = storages.map((storage) => {
    const used = Math.min(Math.max(Number(storage.usagePercent) || 0, 0), 100);
    const tone = used >= 90 ? "bad" : used >= 75 ? "warn" : "";
    return `
      <article class="storage-pie-card">
        <h3 title="${escapeHtml(storage.name)}">${escapeHtml(storage.name)}</h3>
        <div class="muted">${escapeHtml(storage.recordingServerName ?? "")} · ${escapeHtml(storage.kind)}</div>
        <div class="storage-pie ${tone}" style="background: conic-gradient(var(--slice) 0 ${used}%, #e8eef2 ${used}% 100%)">
          <div class="storage-pie-inner">${formatPercent(used)}</div>
        </div>
        <div class="muted">${escapeHtml(storage.usedLabel)} / ${escapeHtml(storage.maxLabel)}</div>
      </article>
    `;
  }).join("") || `<p class="muted">No storage volumes were returned.</p>`;
}

function renderStorage(storages) {
  document.getElementById("storage-list").innerHTML = storages.map((storage) => {
    const tone = storage.usagePercent >= 90 ? "bad" : storage.usagePercent >= 75 ? "warn" : "";
    return `
      <section class="storage-card">
        <header>
          <div>
            <strong>${escapeHtml(storage.name)}</strong>
            <div class="muted">${escapeHtml(storage.recordingServerName ?? "")} · ${escapeHtml(storage.kind)}</div>
          </div>
          <div>${formatPercent(storage.usagePercent)}</div>
        </header>
        <div class="bar ${tone}"><span style="width:${Math.min(storage.usagePercent, 100)}%"></span></div>
        <p class="muted">${escapeHtml(storage.usedLabel)} / ${escapeHtml(storage.maxLabel)} · Retention ${escapeHtml(storage.retentionLabel)}</p>
        <p class="muted">${escapeHtml(storage.diskPath ?? "Path not reported")}${storage.lockedUsedSpaceMb ? ` · Locked ${formatMb(storage.lockedUsedSpaceMb)}` : ""}</p>
      </section>
    `;
  }).join("") || `<p class="muted">No storage volumes were returned.</p>`;
}

function clearCameraFilters({ keepServer = false } = {}) {
  searchInput.value = "";
  if (!keepServer) {
    serverFilter.value = "";
  }
  labelFilter.value = "";
  locationFilter.value = "";
  siteFilter.value = "";
  vendorFilter.value = "";
  lifecycleFilter.value = "";
  ndaaFilter.value = "";
  passwordFilter.value = "";
  firmwareFilter.value = "";
  state.page = 1;
}

function showRecordingServers() {
  state.inventoryView = "servers";
  clearCameraFilters();
  renderOverview();
  renderInventory();
  showView("devices");
}

function showAllCameras() {
  state.inventoryView = "cameras";
  clearCameraFilters();
  renderOverview();
  renderInventory();
  showView("devices");
}

function showCamerasForServer(serverId) {
  state.inventoryView = "cameras";
  clearCameraFilters({ keepServer: true });
  serverFilter.value = serverId;
  renderOverview();
  renderInventory();
  showView("devices");
}

function showCamerasForSite(siteName) {
  state.inventoryView = "cameras";
  serverFilter.value = "";
  siteFilter.value = siteName;
  state.page = 1;
  renderOverview();
  renderInventory();
  showView("devices");
}

function showCamerasForLifecycle(filter, extras = {}) {
  state.inventoryView = "cameras";
  clearCameraFilters();
  if (extras.site) {
    siteFilter.value = extras.site;
  }
  if (extras.query) {
    searchInput.value = extras.query;
  }
  if (extras.ndaa) {
    ndaaFilter.value = extras.ndaa;
  }
  if (extras.password) {
    passwordFilter.value = extras.password;
  }
  if (extras.firmware) {
    firmwareFilter.value = extras.firmware;
  }
  lifecycleFilter.value = filter ?? "";
  renderOverview();
  renderInventory();
  showView("devices");
}

function renderInventory() {
  const serversWrap = document.getElementById("servers-wrap");
  const camerasWrap = document.getElementById("cameras-wrap");
  const filters = document.getElementById("camera-filters");
  const title = document.getElementById("inventory-title");
  const copy = document.getElementById("inventory-copy");
  const showingServers = state.inventoryView === "servers";
  serversWrap.hidden = !showingServers;
  camerasWrap.hidden = showingServers;
  filters.hidden = showingServers;

  if (showingServers) {
    title.textContent = "Recording servers";
    copy.innerHTML = `${state.recordingServers.length} recording servers from XProtect. Click a server name to see its cameras.`;
    document.getElementById("tab-cameras").classList.remove("active");
    document.getElementById("tab-servers").classList.add("active");
    document.getElementById("camera-footer").hidden = true;
    renderServers();
    return;
  }

  const selected = state.recordingServers.find((server) => server.id === serverFilter.value);
  const selectedSite = siteFilter.value;
  const selectedLife = lifecycleFilter.value;
  const selectedNdaa = ndaaFilter.value;
  const selectedPassword = passwordFilter.value;
  const selectedFirmware = firmwareFilter.value;
  title.textContent = "Device Management";
  copy.innerHTML = selected
    ? `${visibleCameras().length} cameras on ${escapeHtml(selected.name)}. <button class="link-btn" type="button" id="back-to-servers">Back to servers</button> · <button class="link-btn" type="button" id="show-all-cameras">Show all cameras</button>`
    : selectedSite || selectedLife || selectedNdaa || selectedPassword || selectedFirmware
      ? `${visibleCameras().length} cameras${selectedSite ? ` at ${escapeHtml(selectedSite)}` : ""}${selectedLife ? ` · ${escapeHtml(lifecycleLabel(selectedLife))}` : ""}${selectedNdaa ? ` · NDAA ${escapeHtml(ndaaLabel(selectedNdaa))}` : ""}${selectedPassword ? ` · ${escapeHtml(passwordLabel(selectedPassword))}` : ""}${selectedFirmware ? ` · ${escapeHtml(firmwareLabel(selectedFirmware))}` : ""}. <button class="link-btn" type="button" id="show-all-cameras">Show all cameras</button>`
      : "All cameras from XProtect.";
  document.getElementById("tab-cameras").classList.add("active");
  document.getElementById("tab-servers").classList.remove("active");
  document.getElementById("camera-footer").hidden = false;
  renderCameras();
  document.getElementById("back-to-servers")?.addEventListener("click", showRecordingServers);
  document.getElementById("show-all-cameras")?.addEventListener("click", showAllCameras);
}

function renderServers() {
  const rows = [...state.recordingServers].sort((left, right) => left.name.localeCompare(right.name));
  document.getElementById("server-body").innerHTML = rows.map((server) => {
    const usage = server.maxSizeMb > 0 ? (server.usedSpaceMb * 100) / server.maxSizeMb : 0;
    return `
    <tr>
      <td><button class="link-btn" type="button" data-server="${escapeHtml(server.id)}">${escapeHtml(server.name)}</button></td>
      <td>${escapeHtml(server.hostName ?? "—")}</td>
      <td><span class="status ${server.enabled === false ? "off" : "on"}">${server.enabled === false ? "Offline" : "Online"}</span></td>
      <td><button class="link-btn" type="button" data-server="${escapeHtml(server.id)}" title="Show cameras on this server">${server.cameraCount ?? 0}</button></td>
      <td>${escapeHtml(formatMb(server.usedSpaceMb))}</td>
      <td>${escapeHtml(formatMb(server.maxSizeMb))}</td>
      <td>${formatPercent(usage)}</td>
    </tr>
  `;
  }).join("") || `<tr><td colspan="7">No recording servers were returned.</td></tr>`;

  document.querySelectorAll("[data-server]").forEach((button) => {
    button.addEventListener("click", () => showCamerasForServer(button.getAttribute("data-server") ?? ""));
  });
}

function fillServerFilter(servers) {
  const current = serverFilter.value;
  serverFilter.innerHTML = `<option value="">Recording server</option>` +
    servers.map((server) => `<option value="${escapeHtml(server.id)}">${escapeHtml(server.name)}</option>`).join("");
  serverFilter.value = current;
}

function fillLabelFilter(cameras) {
  const current = labelFilter.value;
  const labels = [...new Set(cameras.flatMap((camera) => camera.labels ?? []))].sort((left, right) =>
    left.localeCompare(right));
  labelFilter.innerHTML = `<option value="">Group</option>` +
    labels.map((label) => `<option value="${escapeHtml(label)}">${escapeHtml(label)}</option>`).join("");
  labelFilter.value = labels.includes(current) ? current : "";
}

function fillChoiceFilter(select, values, placeholder) {
  const current = select.value;
  const options = [...new Set(values.filter(Boolean))].sort((left, right) => left.localeCompare(right));
  select.innerHTML = `<option value="">${placeholder}</option>` +
    options.map((value) => `<option value="${escapeHtml(value)}">${escapeHtml(value)}</option>`).join("");
  select.value = options.includes(current) ? current : "";
}

function resetPageAndRender() {
  state.page = 1;
  renderCameras();
}

function fillPlaceCamera() {
  const current = state.placingCameraId;
  placeCamera.innerHTML = `<option value="">Select camera to place</option>` +
    state.cameras.map((camera) => {
      const suffix = camera.location ? " (mapped)" : "";
      return `<option value="${escapeHtml(camera.id)}">${escapeHtml(camera.name)}${suffix}</option>`;
    }).join("");
  placeCamera.value = current;
  state.placingCameraId = placeCamera.value;
  updatePlaceHint();
}

function visibleCameras() {
  const query = searchInput.value.trim().toLowerCase();
  return state.cameras.filter((camera) => {
    const haystack = [
      camera.name,
      camera.shortName,
      camera.description,
      camera.site,
      camera.recordingServerName,
      camera.hardwareAddress,
      camera.model,
      camera.firmware,
      camera.serialNumber,
      camera.macAddress,
      camera.hardwareDriver,
      camera.hardwareUserName,
      camera.vendor,
      camera.ipAddress,
      camera.deviceSource,
      camera.intelligence?.lifecycleStatus,
      camera.intelligence?.ndaaStatus,
      camera.intelligence?.replacementModel,
      camera.intelligence?.suggestedFirmware,
      camera.intelligence?.recordingStatus,
      camera.intelligence?.storageServer,
      camera.intelligence?.sdStatus,
      ...(camera.labels ?? []),
      ...Object.entries(camera.customProperties ?? {}).flatMap(([key, value]) => [key, value])
    ].filter(Boolean).join(" ").toLowerCase();
    const matchesQuery = !query || haystack.includes(query);
    const selectedServer = state.recordingServers.find((server) => server.id === serverFilter.value);
    const matchesServer = !serverFilter.value
      || camera.recordingServerId === serverFilter.value
      || (selectedServer && camera.recordingServerName === selectedServer.name);
    const matchesLabel = !labelFilter.value || (camera.labels ?? []).includes(labelFilter.value);
    const matchesSite = !siteFilter.value || camera.site === siteFilter.value;
    const matchesVendor = !vendorFilter.value || camera.vendor === vendorFilter.value;
    const matchesLifecycle = matchesLifecycleFilter(camera, lifecycleFilter.value);
    const matchesNdaa = matchesNdaaFilter(camera, ndaaFilter.value);
    const matchesPassword = matchesPasswordFilter(camera, passwordFilter.value);
    const matchesFirmware = matchesFirmwareFilter(camera, firmwareFilter.value);
    const mapped = Boolean(camera.location);
    const matchesLocation = !locationFilter.value
      || (locationFilter.value === "mapped" && mapped)
      || (locationFilter.value === "unmapped" && !mapped);
    return matchesQuery && matchesServer && matchesLabel && matchesSite && matchesVendor && matchesLifecycle && matchesNdaa && matchesPassword && matchesFirmware && matchesLocation;
  });
}

function pagedCameras() {
  const rows = visibleCameras();
  const pageSize = state.pageSize || 100;
  const pageCount = Math.max(1, Math.ceil(rows.length / pageSize));
  state.page = Math.min(Math.max(state.page, 1), pageCount);
  const start = (state.page - 1) * pageSize;
  return {
    rows,
    pageRows: rows.slice(start, start + pageSize),
    start,
    pageCount,
    pageSize
  };
}

function renderCameras() {
  const { rows, pageRows, start, pageCount, pageSize } = pagedCameras();
  const selectedTotal = rows.filter((camera) => state.selectedCameraIds.has(camera.id)).length;
  const selectedOnPage = pageRows.filter((camera) => state.selectedCameraIds.has(camera.id)).length;
  selectAll.checked = pageRows.length > 0 && selectedOnPage === pageRows.length;
  selectAll.indeterminate = selectedOnPage > 0 && selectedOnPage < pageRows.length;
  document.getElementById("selection-summary").textContent =
    `${state.selectedCameraIds.size} selected out of ${rows.length.toLocaleString()} items`;
  const first = rows.length === 0 ? 0 : start + 1;
  const last = Math.min(start + pageSize, rows.length);
  document.getElementById("page-range").textContent =
    `${first}-${last} of ${rows.length.toLocaleString()} items`;
  renderPager(pageCount);

  document.getElementById("camera-body").innerHTML = pageRows.map((camera) => {
    const expanded = camera.id === state.expandedCameraId;
    const selected = state.selectedCameraIds.has(camera.id);
    const mgmt = camera.hardwareEnabled !== false && camera.enabled;
    const app = camera.enabled;
    const edge = Boolean(camera.edgeStorageEnabled);
    const credentials = Boolean(camera.hardwareUserName);
    const intel = camera.intelligence ?? {};
    return `
    <tr class="camera-row ${expanded ? "active" : ""} ${selected ? "selected" : ""}" data-toggle="${escapeHtml(camera.id)}">
      <td class="check-col"><input type="checkbox" data-select="${escapeHtml(camera.id)}" ${selected ? "checked" : ""} /></td>
      <td>
        <div class="device-status">
          <span class="status-dot ${camera.enabled ? "ok" : "off"}" title="${camera.enabled ? "Enabled" : "Disabled"}"></span>
          <span class="conn-tag ${mgmt ? "on" : "off"}" title="Hardware reachable in XProtect">MGMT</span>
          <span class="conn-tag ${app ? "on" : "off"}" title="Camera enabled in XProtect">APP</span>
          <span class="conn-tag ${edge ? "on" : "off"}" title="Edge storage">EDGE</span>
        </div>
      </td>
      <td>${intel.alertStatus ? escapeHtml(intel.alertStatus) : ""}</td>
      <td class="name-cell">${escapeHtml(camera.name)}</td>
      <td>
        <span class="cred ${credentials ? "ok" : "off"}" title="${credentials ? `User ${camera.hardwareUserName}` : "No hardware user"}">${credentials ? "✓" : "!"}</span>
      </td>
      <td><span class="type-cam" title="Camera"></span></td>
      <td>
        <div class="ops">
          <button class="op-btn" type="button" data-toggle="${escapeHtml(camera.id)}" title="${credentials ? "Credentials configured" : "Credentials not reported"}">${iconLock()}</button>
          <button class="op-btn ${camera.firmware ? "" : "off"}" type="button" data-toggle="${escapeHtml(camera.id)}" title="${camera.firmware ? `Firmware ${camera.firmware}` : "Firmware not reported"}">${iconChip()}</button>
          <button class="op-btn" type="button" data-place="${escapeHtml(camera.id)}" title="Place on map">${iconPin()}</button>
          <button class="op-btn ${camera.recordingEnabled === false ? "off" : ""}" type="button" data-toggle="${escapeHtml(camera.id)}" title="${camera.recordingEnabled === false ? "Recording disabled" : "Recording enabled"}">${iconShield()}</button>
        </div>
      </td>
      <td>${escapeHtml(camera.site ?? "—")}</td>
      <td>${escapeHtml(camera.deviceSource ?? state.sourceLabel ?? "Milestone")}</td>
      <td>${renderChips(shortLabels(camera.labels))}</td>
      <td class="notes-cell">${escapeHtml(camera.description || "—")}</td>
      <td>${escapeHtml(camera.vendor ?? "—")}</td>
      <td>${escapeHtml(displayModel(camera))}</td>
      <td>${escapeHtml(camera.ipAddress ?? "—")}</td>
      <td>${escapeHtml(formatLastSeen(camera.lastModified))}</td>
      <td>${escapeHtml(camera.firmware ?? "—")}</td>
      <td>${severityCell(intel.vulnerabilitySeverity)}</td>
      <td>${escapeHtml(intel.patchedFirmware ?? camera.firmware ?? "—")}</td>
      <td>${escapeHtml(intel.suggestedFirmware ?? "—")}</td>
      <td>${escapeHtml(formatLastSeen(intel.lastFirmwareUpgrade))}</td>
      <td>${lifecycleCell(intel.lifecycleStatus)}</td>
      <td>${escapeHtml(formatDateOnly(intel.eosDate))}</td>
      <td class="notes-cell" title="${escapeHtml(intel.replacementModel ?? "")}">${escapeHtml(intel.replacementModel ?? "—")}</td>
      <td>${dotLabel(intel.warrantyStatus, "na")}</td>
      <td>${escapeHtml(formatDateOnly(intel.warrantyDate))}</td>
      <td>${dotLabel(intel.ndaaStatus, ndaaTone(intel.ndaaStatus))}</td>
      <td>${dotLabel(intel.passwordExpiryStatus, passwordTone(intel.passwordExpiryStatus))}</td>
      <td>${escapeHtml(formatExpiry(intel.passwordExpiryDate, intel.passwordExpiryStatus))}</td>
      <td>${dotLabel(intel.sslExpiryStatus, "na")}</td>
      <td>${escapeHtml(formatDateOnly(intel.sslExpiryDate))}</td>
      <td>${escapeHtml(intel.lastSslCertificate ?? "—")}</td>
      <td>${dotLabel(intel.sslCompliance, intel.sslCompliance === "Non Compliant" ? "bad" : "na")}</td>
      <td>${dotLabel(intel.dot1xStatus, "na")}</td>
      <td>${escapeHtml(intel.lastHardened ?? "N/A")}</td>
      <td>${escapeHtml(intel.recordingStatus ?? "—")}</td>
      <td>${escapeHtml(intel.storageServer ?? camera.recordingServerName ?? "—")}</td>
      <td>${escapeHtml(intel.sdStatus ?? "—")}</td>
      <td>${escapeHtml(intel.sdWearStatus ?? "—")}</td>
    </tr>
    ${expanded ? renderCameraDetails(camera) : ""}
  `;
  }).join("") || `<tr><td colspan="38">No cameras match the current filters.</td></tr>`;

  document.querySelectorAll("[data-select]").forEach((box) => {
    box.addEventListener("click", (event) => event.stopPropagation());
    box.addEventListener("change", (event) => {
      event.stopPropagation();
      const cameraId = box.getAttribute("data-select") ?? "";
      if (box.checked) {
        state.selectedCameraIds.add(cameraId);
      } else {
        state.selectedCameraIds.delete(cameraId);
      }
      renderCameras();
    });
  });

  document.querySelectorAll("[data-place]").forEach((button) => {
    button.addEventListener("click", (event) => {
      event.stopPropagation();
      state.placingCameraId = button.getAttribute("data-place") ?? "";
      placeCamera.value = state.placingCameraId;
      updatePlaceHint();
      showView("dashboard", { focus: "sites-view" });
    });
  });

  document.querySelectorAll("[data-toggle]").forEach((item) => {
    item.addEventListener("click", (event) => {
      event.stopPropagation();
      const cameraId = item.getAttribute("data-toggle") ?? "";
      state.expandedCameraId = state.expandedCameraId === cameraId ? "" : cameraId;
      renderCameras();
    });
  });
}

function renderPager(pageCount) {
  const pages = [];
  const current = state.page;
  const push = (page, label = String(page), active = false) => {
    pages.push(`<button type="button" class="page-btn ${active ? "active" : ""}" data-page="${page}">${label}</button>`);
  };
  push(Math.max(1, current - 1), "‹");
  const windowStart = Math.max(1, Math.min(current - 2, pageCount - 4));
  const windowEnd = Math.min(pageCount, windowStart + 4);
  if (windowStart > 1) {
    push(1);
    if (windowStart > 2) {
      pages.push(`<span class="page-gap">…</span>`);
    }
  }
  for (let page = windowStart; page <= windowEnd; page += 1) {
    push(page, String(page), page === current);
  }
  if (windowEnd < pageCount) {
    if (windowEnd < pageCount - 1) {
      pages.push(`<span class="page-gap">…</span>`);
    }
    push(pageCount);
  }
  push(Math.min(pageCount, current + 1), "›");
  document.getElementById("page-nav").innerHTML = pages.join("");
  document.querySelectorAll("[data-page]").forEach((button) => {
    button.addEventListener("click", () => {
      state.page = Number(button.getAttribute("data-page")) || 1;
      renderCameras();
    });
  });
}

function downloadCameraCsv() {
  const rows = visibleCameras();
  const header = ["cameraId", "name", "site", "vendor", "model", "ipAddress", "firmware", "recordingServer", "labels", "enabled"];
  const lines = [header.join(",")].concat(rows.map((camera) => [
    camera.id,
    camera.name,
    camera.site ?? "",
    camera.vendor ?? "",
    camera.model ?? "",
    camera.ipAddress ?? "",
    camera.firmware ?? "",
    camera.recordingServerName ?? "",
    (camera.labels ?? []).join("|"),
    camera.enabled
  ].map((value) => `"${String(value).replaceAll("\"", "\"\"")}"`).join(",")));
  const blob = new Blob([lines.join("\n")], { type: "text/csv" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "cameras.csv";
  link.click();
  URL.revokeObjectURL(url);
}

function fillManageFilters() {
  fillChoiceFilter(manageSiteFilter, state.sites.map((site) => site.name), "Site Name");
  const currentLabel = manageLabelFilter.value;
  const labels = [...new Set(state.sites.flatMap((site) => site.labels ?? []))].sort((left, right) =>
    left.localeCompare(right));
  manageLabelFilter.innerHTML = `<option value="">Labels</option>` +
    labels.map((label) => `<option value="${escapeHtml(label)}">${escapeHtml(label)}</option>`).join("");
  manageLabelFilter.value = labels.includes(currentLabel) ? currentLabel : "";
}

function setManageMode(mode) {
  state.manageMode = mode === "map" ? "map" : "table";
  document.getElementById("manage-map-btn").classList.toggle("active", state.manageMode === "map");
  document.getElementById("manage-table-btn").classList.toggle("active", state.manageMode === "table");
  document.getElementById("manage-table-wrap").hidden = state.manageMode !== "table";
  document.getElementById("manage-map-wrap").hidden = state.manageMode !== "map";
  document.getElementById("manage-footer").hidden = state.manageMode !== "table";
  if (state.manageMode === "map") {
    renderManageMap(visibleSites());
  }
}

function siteStatusLabel(status) {
  return status === "Partial" ? "Partially Connected" : status || "N/A";
}

function siteStatusClass(status) {
  if (status === "Connected") {
    return "ok";
  }
  if (status === "Disconnected") {
    return "off";
  }
  return status === "Partial" ? "partial" : "na";
}

function visibleSites() {
  return state.sites.filter((site) => {
    const matchesName = !manageSiteFilter.value || site.name === manageSiteFilter.value;
    const matchesStatus = !manageStatusFilter.value || site.status === manageStatusFilter.value;
    const matchesLabel = !manageLabelFilter.value || (site.labels ?? []).includes(manageLabelFilter.value);
    return matchesName && matchesStatus && matchesLabel;
  });
}

function pagedSites() {
  const rows = visibleSites();
  const pageSize = state.managePageSize || 100;
  const pageCount = Math.max(1, Math.ceil(rows.length / pageSize));
  state.managePage = Math.min(Math.max(state.managePage, 1), pageCount);
  const start = (state.managePage - 1) * pageSize;
  return {
    rows,
    pageRows: rows.slice(start, start + pageSize),
    start,
    pageCount,
    pageSize
  };
}

function renderSites() {
  const connected = state.sites.filter((site) => site.status === "Connected").length;
  const disconnected = state.sites.filter((site) => site.status === "Disconnected").length;
  const partial = state.sites.filter((site) => site.status === "Partial").length;
  const unknown = state.sites.filter((site) => site.status === "N/A").length;
  document.getElementById("site-summary").innerHTML = `
    <strong>${state.sites.length} Managed Sites</strong>
    <span class="legend-item"><i class="dot green"></i>Connected (${connected})</span>
    <span class="legend-item"><i class="dot red"></i>Disconnected (${disconnected})</span>
    <span class="legend-item"><i class="dot orange"></i>Partially Connected (${partial})</span>
    <span class="legend-item"><i class="dot gray"></i>N/A (${unknown})</span>
  `;

  const { rows, pageRows, start, pageCount, pageSize } = pagedSites();
  const first = rows.length === 0 ? 0 : start + 1;
  const last = Math.min(start + pageSize, rows.length);
  document.getElementById("manage-page-range").textContent =
    `${first}-${last} of ${rows.length.toLocaleString()} items`;
  renderManagePager(pageCount);

  document.getElementById("site-body").innerHTML = pageRows.map((site) => `
    <tr>
      <td class="site-status"><span class="status-dot ${siteStatusClass(site.status)}" title="${escapeHtml(siteStatusLabel(site.status))}" aria-label="${escapeHtml(siteStatusLabel(site.status))}"></span></td>
      <td><button class="site-name" type="button" data-site="${escapeHtml(site.name)}">${escapeHtml(site.name)}</button></td>
      <td class="notes-cell">${escapeHtml(site.description || "—")}</td>
      <td>${site.managedCount ?? 0}</td>
      <td>${renderMiniCounts(site)}</td>
      <td>${renderSegmentBar([
        ["ok", site.okVulnCount],
        ["warn", site.mediumVulnCount],
        ["bad", site.highVulnCount]
      ], site.managedCount)}</td>
      <td>${renderSegmentBar([
        ["ok", site.currentFirmwareCount],
        ["na", site.unknownFirmwareCount],
        ["bad", site.outdatedFirmwareCount]
      ], site.managedCount)}</td>
      <td>${renderSegmentBar([
        ["ok", site.activeLifecycleCount],
        ["warn", site.eolCount],
        ["bad", site.eosCount]
      ], site.managedCount)}</td>
      <td>${renderChips(site.labels)}</td>
      <td>${escapeHtml(formatDms(site.location))}</td>
      <td><button class="more-btn" type="button" data-site="${escapeHtml(site.name)}" title="Open cameras">${iconSpark()}</button></td>
    </tr>
  `).join("") || `<tr><td colspan="11">No sites match the current filters.</td></tr>`;

  document.querySelectorAll("[data-site]").forEach((button) => {
    button.addEventListener("click", () => showCamerasForSite(button.getAttribute("data-site") ?? ""));
  });

  setManageMode(state.manageMode);
}

function renderMiniCounts(site) {
  return `
    <div class="mini-counts">
      <span><i class="dot green"></i>${site.enabledCount ?? 0}</span>
      <span><i class="dot red"></i>${site.disabledCount ?? 0}</span>
      <span><i class="dot orange"></i>${site.unmappedCount ?? 0}</span>
      <span><i class="dot gray"></i>${site.unknownFirmwareCount ?? 0}</span>
    </div>
  `;
}

function renderSegmentBar(parts, total) {
  const max = Math.max(Number(total) || 0, parts.reduce((sum, [, count]) => sum + (Number(count) || 0), 0), 1);
  const segments = parts
    .filter(([, count]) => Number(count) > 0)
    .map(([tone, count]) => `<span class="${tone}" style="width:${(Number(count) / max) * 100}%"></span>`)
    .join("");
  const title = parts.map(([tone, count]) => `${tone}: ${count ?? 0}`).join(" · ");
  return `<div class="seg-bar" title="${escapeHtml(title)}">${segments}</div>`;
}

function renderManagePager(pageCount) {
  const pages = [];
  const current = state.managePage;
  const push = (page, label = String(page), active = false) => {
    pages.push(`<button type="button" class="page-btn ${active ? "active" : ""}" data-site-page="${page}">${label}</button>`);
  };
  push(Math.max(1, current - 1), "‹");
  push(Math.min(pageCount, current + 1), "›");
  document.getElementById("manage-page-nav").innerHTML = pages.join("");
  document.querySelectorAll("[data-site-page]").forEach((button) => {
    button.addEventListener("click", () => {
      state.managePage = Number(button.getAttribute("data-site-page")) || 1;
      renderSites();
    });
  });
}

function downloadSiteCsv() {
  const rows = visibleSites();
  const header = [
    "siteName", "status", "description", "managed", "enabled", "disabled", "unmapped",
    "highVuln", "mediumVuln", "okVuln", "currentFirmware", "outdatedFirmware",
    "activeLifecycle", "eol", "eos", "labels", "latitude", "longitude"
  ];
  const lines = [header.join(",")].concat(rows.map((site) => [
    site.name,
    siteStatusLabel(site.status),
    site.description ?? "",
    site.managedCount ?? 0,
    site.enabledCount ?? 0,
    site.disabledCount ?? 0,
    site.unmappedCount ?? 0,
    site.highVulnCount ?? 0,
    site.mediumVulnCount ?? 0,
    site.okVulnCount ?? 0,
    site.currentFirmwareCount ?? 0,
    site.outdatedFirmwareCount ?? 0,
    site.activeLifecycleCount ?? 0,
    site.eolCount ?? 0,
    site.eosCount ?? 0,
    (site.labels ?? []).join("|"),
    site.location?.latitude ?? "",
    site.location?.longitude ?? ""
  ].map((value) => `"${String(value).replaceAll("\"", "\"\"")}"`).join(",")));
  const blob = new Blob([lines.join("\n")], { type: "text/csv" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "sites.csv";
  link.click();
  URL.revokeObjectURL(url);
}

function formatDms(location) {
  if (!location || location.latitude == null || location.longitude == null) {
    return "—";
  }
  return `${toDms(location.latitude, "N", "S")}, ${toDms(location.longitude, "E", "W")}`;
}

function toDms(value, positive, negative) {
  const abs = Math.abs(value);
  const degrees = Math.floor(abs);
  const minutesFloat = (abs - degrees) * 60;
  const minutes = Math.floor(minutesFloat);
  const seconds = ((minutesFloat - minutes) * 60).toFixed(2);
  return `${degrees}°${minutes}'${seconds}"${value >= 0 ? positive : negative}`;
}

function iconSpark() {
  return `<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 17h2v4H3v-4zm4-6h2v10H7V11zm4-4h2v14h-2V7zm4 7h2v7h-2v-7zm4-9h2v16h-2V5z"/></svg>`;
}

function renderManageMap(sites) {
  if (typeof L === "undefined") {
    return;
  }

  const mapped = sites.filter((site) => site.location);
  const center = [state.mapCenter.latitude, state.mapCenter.longitude];
  if (!state.manageMap) {
    state.manageMap = L.map("manage-map", { zoomControl: true }).setView(center, state.mapCenter.zoom ?? 13);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors"
    }).addTo(state.manageMap);
    state.manageCluster = L.layerGroup().addTo(state.manageMap);
  }

  state.manageCluster.clearLayers();
  const bounds = [];
  mapped.forEach((site) => {
    const point = [site.location.latitude, site.location.longitude];
    const marker = L.marker(point).bindPopup(
      `<strong>${escapeHtml(site.name)}</strong><br>` +
      `${site.managedCount ?? 0} managed cameras<br>` +
      `${escapeHtml(siteStatusLabel(site.status))}`
    );
    marker.on("click", () => {
      marker.openPopup();
    });
    state.manageCluster.addLayer(marker);
    bounds.push(point);
  });

  if (bounds.length > 0) {
    state.manageMap.fitBounds(bounds, { padding: [28, 28], maxZoom: 16 });
  } else {
    state.manageMap.setView(center, state.mapCenter.zoom ?? 13);
  }
  setTimeout(() => state.manageMap.invalidateSize(), 80);
}

function shortLabels(values) {
  return (values ?? []).map((value) => {
    const parts = String(value).split(" / ").map((part) => part.trim()).filter(Boolean);
    return parts.at(-1) ?? value;
  });
}

function displayModel(camera) {
  const model = camera.model ?? "";
  const vendor = camera.vendor ?? "";
  if (vendor && model.toLowerCase().startsWith(vendor.toLowerCase())) {
    return model.slice(vendor.length).replace(/^[\s-]+/, "") || model;
  }
  return model || "—";
}

function severityCell(value) {
  if (!value) {
    return `<span class="muted">N/A</span>`;
  }
  const tone = value === "High" ? "bad" : value === "Medium" ? "warn" : "ok";
  return `<span class="severity ${tone}" title="From product support status, not a live CVE scan">${escapeHtml(value)}</span>`;
}

function lifecycleCell(value) {
  return dotLabel(value, value === "EOS" ? "bad" : value === "EOL" ? "warn" : value === "Active" ? "ok" : "na");
}

function ndaaTone(value) {
  if (value === "Compliant") {
    return "ok";
  }
  return value === "Restricted" ? "bad" : "na";
}

function passwordTone(value) {
  if (value === "Up To Date") {
    return "ok";
  }
  if (value === "Due Soon") {
    return "warn";
  }
  if (value === "Never Rotated") {
    return "warn";
  }
  return value === "Overdue" ? "bad" : "na";
}

function dotLabel(value, tone) {
  if (!value) {
    return `<span class="life na"><i></i>N/A</span>`;
  }
  return `<span class="life ${tone}"><i></i>${escapeHtml(value)}</span>`;
}

function formatDateOnly(value) {
  if (!value) {
    return "N/A";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "N/A";
  }
  const pad = (number) => String(number).padStart(2, "0");
  return `${pad(date.getMonth() + 1)}/${pad(date.getDate())}/${date.getFullYear()}`;
}

function formatExpiry(value, status) {
  if (!value) {
    return "N/A";
  }
  const stamp = formatLastSeen(value);
  if (status === "Up To Date") {
    return `${stamp} | Within policy`;
  }
  return stamp;
}

function formatLastSeen(value) {
  if (!value) {
    return "—";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "—";
  }
  const pad = (number) => String(number).padStart(2, "0");
  return `${pad(date.getMonth() + 1)}/${pad(date.getDate())}/${String(date.getFullYear()).slice(-2)} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

function iconLock() {
  return `<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M7 10V8a5 5 0 0 1 10 0v2h1.5A1.5 1.5 0 0 1 20 11.5v8A1.5 1.5 0 0 1 18.5 21h-13A1.5 1.5 0 0 1 4 19.5v-8A1.5 1.5 0 0 1 5.5 10H7zm2 0h6V8a3 3 0 0 0-6 0v2z"/></svg>`;
}

function iconChip() {
  return `<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 3h2v3h2V3h2v3h3v3h3v2h-3v2h3v2h-3v3h-3v3h-2v-3h-2v3H9v-3H6v-3H3v-2h3v-2H3V9h3V6h3V3zm1 7v4h4v-4h-4z"/></svg>`;
}

function iconPin() {
  return `<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 2a7 7 0 0 1 7 7c0 5.25-7 13-7 13S5 14.25 5 9a7 7 0 0 1 7-7zm0 4.5A2.5 2.5 0 1 0 12 11a2.5 2.5 0 0 0 0-4.5z"/></svg>`;
}

function iconShield() {
  return `<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 2 20 6v6c0 5-3.4 8.4-8 10-4.6-1.6-8-5-8-10V6l8-4z"/></svg>`;
}

function renderChips(values) {
  if (!values?.length) {
    return "—";
  }

  return `<div class="chips">${values.map((value) => `<span class="chip">${escapeHtml(value)}</span>`).join("")}</div>`;
}

function renderCameraDetails(camera) {
  const properties = Object.entries(camera.customProperties ?? {});
  const fields = [
    ["Camera ID", camera.id],
    ["Short name", camera.shortName],
    ["Description", camera.description],
    ["Channel", camera.channel],
    ["Vendor", camera.vendor],
    ["Model", camera.model],
    ["IP address", camera.ipAddress],
    ["Device source", camera.deviceSource],
    ["Firmware", camera.firmware],
    ["Suggested FW", camera.intelligence?.suggestedFirmware],
    ["Lifecycle", camera.intelligence?.lifecycleStatus],
    ["EOS date", formatDateOnly(camera.intelligence?.eosDate)],
    ["Replacement", camera.intelligence?.replacementModel],
    ["NDAA", camera.intelligence?.ndaaStatus],
    ["SSL compliance", camera.intelligence?.sslCompliance],
    ["Recording", camera.intelligence?.recordingStatus],
    ["Storage server", camera.intelligence?.storageServer],
    ["SD status", camera.intelligence?.sdStatus],
    ["Password policy", camera.intelligence?.passwordExpiryStatus],
    ["Serial number", camera.serialNumber],
    ["MAC address", camera.macAddress],
    ["Hardware", camera.hardwareName],
    ["Hardware address", camera.hardwareAddress],
    ["Hardware user", camera.hardwareUserName],
    ["Hardware enabled", formatYesNo(camera.hardwareEnabled)],
    ["Driver", camera.hardwareDriver],
    ["Recording server", camera.recordingServerName],
    ["Recording storage", camera.recordingStorageName],
    ["Failover", camera.failoverSetting],
    ["Recording", formatYesNo(camera.recordingEnabled)],
    ["Edge storage", formatYesNo(camera.edgeStorageEnabled)],
    ["Edge playback", formatYesNo(camera.edgeStoragePlaybackEnabled)],
    ["Prebuffer", camera.prebufferEnabled == null ? null : `${formatYesNo(camera.prebufferEnabled)}${camera.prebufferSeconds ? ` (${camera.prebufferSeconds}s)` : ""}`],
    ["PTZ", formatYesNo(camera.ptzEnabled)],
    ["Created", formatDate(camera.createdDate)],
    ["Last modified", formatDate(camera.lastModified)],
    ["Password changed", formatDate(camera.passwordLastModified)],
    ["Coordinates", formatLocation(camera)]
  ];

  return `
    <tr class="detail-row">
      <td colspan="38">
        <div class="detail-grid">
          ${fields.filter(([, value]) => value !== null && value !== undefined && value !== "").map(([label, value]) => `
            <div class="detail-item">
              <div class="label">${escapeHtml(label)}</div>
              <div>${escapeHtml(value)}</div>
            </div>
          `).join("")}
        </div>
        ${properties.length ? `
          <h3>Custom properties</h3>
          <div class="detail-grid">
            ${properties.map(([key, value]) => `
              <div class="detail-item">
                <div class="label">${escapeHtml(key)}</div>
                <div>${escapeHtml(value)}</div>
              </div>
            `).join("")}
          </div>
        ` : ""}
        ${camera.labels?.length ? `<h3>Labels</h3>${renderChips(camera.labels)}` : ""}
      </td>
    </tr>
  `;
}

function renderMap(cameras) {
  const mapCopy = document.getElementById("map-copy");
  if (typeof L === "undefined") {
    mapCopy.textContent = "Map library did not load. Cameras still appear in the table below.";
    return;
  }

  const center = [state.mapCenter.latitude, state.mapCenter.longitude];
  if (!state.map) {
    state.map = L.map("map", { zoomControl: true }).setView(center, state.mapCenter.zoom ?? 13);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap contributors"
    }).addTo(state.map);
    state.cluster = L.layerGroup().addTo(state.map);
    state.map.on("click", onMapClick);
  }

  state.cluster.clearLayers();
  const bounds = [];

  cameras.filter((camera) => camera.location).forEach((camera) => {
    const point = [camera.location.latitude, camera.location.longitude];
    const marker = L.marker(point).bindPopup(
      `<strong>${escapeHtml(camera.name)}</strong><br>` +
      `${escapeHtml(camera.model ?? "Unknown model")}${camera.firmware ? ` · ${escapeHtml(camera.firmware)}` : ""}<br>` +
      `${escapeHtml((camera.labels ?? []).join(", ") || camera.site || "")}<br>` +
      `${escapeHtml(camera.recordingServerName ?? "")}`
    );
    state.cluster.addLayer(marker);
    bounds.push(point);
  });

  mapCopy.textContent = bounds.length
    ? `${bounds.length} cameras are on the map. Select another camera and click to move or add a pin.`
    : "No camera coordinates yet. Search a place, choose a camera, then click the map.";

  if (bounds.length > 0) {
    state.map.fitBounds(bounds, { padding: [28, 28], maxZoom: 16 });
  } else {
    state.map.setView(center, state.mapCenter.zoom ?? 13);
  }

  setTimeout(() => state.map.invalidateSize(), 80);
}

async function onMapClick(event) {
  if (!state.placingCameraId) {
    placeHint.textContent = "Select a camera first, then click the map.";
    return;
  }

  const camera = state.cameras.find((item) => item.id === state.placingCameraId);
  const response = await fetch("/api/locations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      cameraId: state.placingCameraId,
      latitude: event.latlng.lat,
      longitude: event.latlng.lng,
      site: camera?.site ?? ""
    })
  });

  if (!response.ok) {
    placeHint.textContent = "Could not save that camera location.";
    return;
  }

  placeHint.textContent = `Saved ${camera?.name ?? "camera"} on the map.`;
  await loadDashboard();
}

async function searchAddress() {
  const query = addressSearch.value.trim();
  if (!query) {
    return;
  }

  const response = await fetch(`/api/geocode?q=${encodeURIComponent(query)}`);
  if (!response.ok) {
    placeHint.textContent = "Address search failed. Check internet access from the web server.";
    return;
  }

  const results = await response.json();
  if (!results.length) {
    placeHint.textContent = "No matching address was found.";
    return;
  }

  const match = results[0];
  state.map.setView([match.latitude, match.longitude], 16);
  placeHint.textContent = `Map moved to ${match.label}. Select a camera and click to pin it.`;
}

async function importCsv(event) {
  const file = event.target.files?.[0];
  event.target.value = "";
  if (!file) {
    return;
  }

  placeHint.textContent = "Importing camera locations…";
  const text = await file.text();
  const rows = parseCsv(text);
  if (rows.length === 0) {
    placeHint.textContent = "No usable coordinates were found. The CSV needs cameraId, latitude, and longitude, with numbers filled in.";
    return;
  }

  const replace = document.getElementById("csv-replace")?.checked !== false;
  let response = await fetch(`/api/locations/import?replace=${replace}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(rows)
  });

  if (response.status === 404 || response.status === 405) {
    const form = new FormData();
    form.append("file", file, file.name);
    response = await fetch(`/api/locations/import-csv?replace=${replace}`, { method: "POST", body: form });
  }

  const raw = await response.text();
  let payload = {};
  try {
    payload = JSON.parse(raw);
  } catch {
    payload = { error: raw ? raw.replace(/<[^>]+>/g, " ").trim().slice(0, 180) : `HTTP ${response.status}` };
  }

  if (!response.ok) {
    placeHint.textContent = payload.error || `CSV import failed (HTTP ${response.status}).`;
    return;
  }

  const extra = payload.unmatched?.length ? ` Unmatched: ${payload.unmatched.slice(0, 8).join(", ")}` : "";
  const skipped = payload.skipped ? ` Skipped ${payload.skipped} rows without coordinates.` : "";
  const invalid = payload.invalid?.length ? ` Skipped ${payload.invalid.length} invalid coordinate row(s).` : "";
  const removed = payload.removed ? ` Removed ${payload.removed} previous pin(s) that were not in this file.` : "";
  const cameras = payload.cameraCount
    ? ` XProtect still has ${payload.cameraCount} cameras. The CSV only updates locations, it does not add or delete cameras.`
    : "";
  placeHint.textContent = `Imported ${payload.saved} camera location(s).${removed}${cameras}${skipped}${invalid}${extra}`;
  await loadDashboard();
}

function parseCsv(text) {
  const lines = text.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
  if (lines.length < 2) {
    return [];
  }

  const headers = splitCsvLine(lines[0]).map((header) => header.replace(/^\uFEFF/, "").toLowerCase());
  const index = (name) => headers.indexOf(name);
  const headerIndex = (names, ...aliases) => {
    const keys = [names, ...aliases];
    for (const key of keys) {
      const found = headers.indexOf(key);
      if (found >= 0) {
        return found;
      }
    }
    return -1;
  };
  return lines.slice(1).map((line) => {
    const cells = splitCsvLine(line);
    const latitude = repairCoord(Number(cells[index("latitude")]), -90, 90);
    const longitude = repairCoord(Number(cells[index("longitude")]), -180, 180);
    if (latitude == null || longitude == null) {
      return null;
    }

    return {
      cameraId: cells[index("cameraid")] || "",
      name: cells[index("name")] || "",
      latitude,
      longitude,
      site: cells[index("site")] || "",
      address: cells[headerIndex(headers, "address")] || "",
      siteName: cells[headerIndex(headers, "site_name", "sitename")] || ""
    };
  }).filter(Boolean);
}

function repairCoord(value, min, max) {
  if (!Number.isFinite(value)) {
    return null;
  }
  if (value >= min && value <= max) {
    return value;
  }

  const sign = value < 0 ? -1 : 1;
  const digits = String(Math.abs(Math.trunc(value)));
  for (const whole of [2, 3, 1]) {
    if (digits.length - whole < 4) {
      continue;
    }
    const repaired = sign * Number(`${digits.slice(0, whole)}.${digits.slice(whole)}`);
    if (repaired >= min && repaired <= max) {
      return repaired;
    }
  }
  return null;
}

function splitCsvLine(line) {
  const values = [];
  let current = "";
  let quoted = false;
  for (const character of line) {
    if (character === "\"") {
      quoted = !quoted;
      continue;
    }
    if (character === "," && !quoted) {
      values.push(current.trim());
      current = "";
      continue;
    }
    current += character;
  }
  values.push(current.trim());
  return values;
}

function updatePlaceHint() {
  const map = document.getElementById("map");
  if (!state.placingCameraId) {
    placeHint.textContent = "Select a camera, then click the map.";
    map.classList.remove("placing");
    return;
  }

  const camera = state.cameras.find((item) => item.id === state.placingCameraId);
  placeHint.textContent = `Click the map to place ${camera?.name ?? "the selected camera"}.`;
  map.classList.add("placing");
}

function formatYesNo(value) {
  if (value == null) {
    return null;
  }
  return value ? "Yes" : "No";
}

function formatDate(value) {
  if (!value) {
    return null;
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function formatLocation(camera) {
  if (!camera.location) {
    return "Not mapped";
  }

  const suffix = camera.locationIsOverride ? " (override)" : "";
  return `${camera.location.latitude.toFixed(5)}, ${camera.location.longitude.toFixed(5)}${suffix}`;
}

function formatPercent(value) {
  return `${Number(value ?? 0).toFixed(1)}%`;
}

function formatCount(value) {
  const number = Number(value ?? 0);
  return number >= 1000 ? `${(number / 1000).toFixed(number >= 10000 ? 0 : 1)}K` : String(number);
}

function formatMb(value) {
  const mb = Number(value ?? 0);
  if (mb < 1024) {
    return `${mb.toLocaleString()} MB`;
  }
  const gb = mb / 1024;
  return gb < 1024 ? `${gb.toFixed(1)} GB` : `${(gb / 1024).toFixed(2)} TB`;
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

loadDashboard();
