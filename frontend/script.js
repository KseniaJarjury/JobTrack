const LEGACY_DATABASE_NAME = "jobtrack";
const LEGACY_DATABASE_VERSION = 1;
const LEGACY_STORE_NAME = "applications";
const MIGRATION_KEY = "jobtrack-local-data-migrated";
const API_BASE_URL = "";
const MAX_FILE_SIZE = 10 * 1024 * 1024;
const allowedExtensions = new Set(["pdf", "doc", "docx", "odt"]);
const fileFields = [
    { key: "resume", inputId: "resumeFile", currentId: "resumeCurrent", label: "CV" },
    { key: "coverLetter", inputId: "coverLetterFile", currentId: "coverLetterCurrent", label: "Carta" }
];

const elements = {
    form: document.getElementById("jobForm"),
    formSection: document.getElementById("applicationForm"),
    formTitle: document.getElementById("formTitle"),
    formError: document.getElementById("formError"),
    saveButton: document.getElementById("saveButton"),
    table: document.getElementById("applicationsTable"),
    emptyState: document.getElementById("emptyState"),
    search: document.getElementById("searchApplications"),
    statusFilter: document.getElementById("statusFilter")
};

let applications = [];
let interviews = [];
let removedFiles = new Set();
let objectUrls = [];
let activityPeriod = "day";
let historyApplicationId = "";
let isReadOnlyFallback = false;
let csrfToken = "";

function openLegacyDatabase() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(LEGACY_DATABASE_NAME, LEGACY_DATABASE_VERSION);
        request.onupgradeneeded = () => {
            if (!request.result.objectStoreNames.contains(LEGACY_STORE_NAME)) {
                request.result.createObjectStore(LEGACY_STORE_NAME, { keyPath: "id" });
            }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error || new Error("No se pudo abrir la base de datos."));
    });
}

function getLegacyApplications(database) {
    return new Promise((resolve, reject) => {
        const request = database.transaction(LEGACY_STORE_NAME, "readonly").objectStore(LEGACY_STORE_NAME).getAll();
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error || new Error("No se pudieron leer las postulaciones."));
    });
}

async function apiRequest(url, options = {}) {
    const headers = new Headers(options.headers || {});
    if (options.method && !["GET", "HEAD", "OPTIONS"].includes(options.method.toUpperCase()) && csrfToken) {
        headers.set("X-CSRF-TOKEN", csrfToken);
    }
    const response = await fetch(`${API_BASE_URL}${url}`, {
        ...options,
        headers,
        credentials: "same-origin"
    });
    if (!response.ok) {
        if (response.status === 401 && !url.startsWith("/api/auth/")) {
            applications = [];
            interviews = [];
            csrfToken = "";
            showAuthentication("login", "Tu sesión venció. Volvé a ingresar para seguir usando JobTrack.");
            try {
                await refreshCsrfToken();
            } catch (error) {
                console.error("No se pudo renovar el token de seguridad:", error);
                showAuthentication("login", `Tu sesión venció y no se pudo renovar la protección de acceso. Recargá la página. Detalle: ${error.message}`);
            }
        }
        let message = `La API respondió con el estado ${response.status}.`;
        try {
            const error = await response.json();
            const validationError = error.errors ? Object.values(error.errors).flat()[0] : "";
            message = error.message || error.title || validationError || message;
        } catch {
            // Conserva el mensaje HTTP cuando la respuesta no contiene JSON.
        }
        const error = new Error(message);
        error.status = response.status;
        throw error;
    }
    if (response.status === 204) return null;
    return response.json();
}

async function refreshCsrfToken() {
    const result = await apiRequest("/api/auth/csrf");
    csrfToken = result.requestToken;
}

function showAuthentication(mode, message = "") {
    document.getElementById("authScreen").hidden = false;
    document.getElementById("appContainer").hidden = true;
    const setupMode = mode === "setup";
    document.getElementById("loginForm").hidden = setupMode;
    document.getElementById("setupForm").hidden = !setupMode;
    document.getElementById("authTitle").textContent = setupMode ? "Creá tu cuenta" : "Iniciá sesión";
    document.getElementById("authDescription").textContent = setupMode
        ? "Configurá tu cuenta personal para proteger tus postulaciones."
        : "Ingresá para ver y organizar tus postulaciones.";
    document.getElementById("loginError").hidden = true;
    document.getElementById("setupError").hidden = true;
    if (message) {
        const error = document.getElementById(setupMode ? "setupError" : "loginError");
        error.textContent = message;
        error.hidden = false;
    }
}

async function loadAuthenticatedApp(email) {
    document.getElementById("authScreen").hidden = true;
    document.getElementById("appContainer").hidden = false;
    const accountLabel = document.getElementById("accountLabel");
    accountLabel.textContent = email;
    accountLabel.title = email;

    try {
        await migrateLegacyApplications();
        applications = await getApplications();
        await getInterviews();
        render();
        resetForm();
        const connectionStatus = document.getElementById("connectionStatus");
        connectionStatus.textContent = "Conectada a la API de JobTrack.";
        connectionStatus.className = "connection-status connected";
        connectionStatus.hidden = false;
    } catch (error) {
        console.error("No se pudo cargar JobTrack:", error);
        const connectionStatus = document.getElementById("connectionStatus");
        try {
            const legacyDatabase = await openLegacyDatabase();
            try {
                applications = await getLegacyApplications(legacyDatabase);
            } finally {
                legacyDatabase.close();
            }
            isReadOnlyFallback = true;
            render();
            resetForm();
            document.getElementById("addJobButton").disabled = true;
            document.getElementById("addJobButtonSecondary").disabled = true;
            document.getElementById("emptyAddButton").disabled = true;
            connectionStatus.textContent = `Sin conexión con la API. Se muestran ${applications.length} datos locales en modo de solo lectura; iniciá el backend para sincronizarlos. Detalle: ${error.message}`;
        } catch (localError) {
            console.error("No se pudieron leer los datos locales:", localError);
            connectionStatus.textContent = `No se pudo conectar con la API ni recuperar los datos locales. Detalle: ${error.message}`;
        }
        connectionStatus.className = "connection-status disconnected";
        connectionStatus.hidden = false;
    }
}

function normalizeApplication(application) {
    const documents = (application.documents || []).map(document => ({
        ...document,
        downloadUrl: `${API_BASE_URL}/api/applications/${application.id}/documents/${document.id}`
    }));
    return {
        ...application,
        resume: documents.find(document => document.type === "CV") || null,
        coverLetter: documents.find(document => document.type === "CoverLetter") || null
    };
}

async function getApplications() {
    const result = await apiRequest("/api/applications");
    return result.map(normalizeApplication);
}

async function getInterviews() {
    interviews = await apiRequest("/api/interviews");
}

function applicationFormData(application, id = "", removed = new Set()) {
    const formData = new FormData();
    const fields = {
        id,
        company: application.company,
        companyDetails: application.companyDetails,
        companyWebsiteUrl: application.companyWebsiteUrl,
        position: application.position,
        applicationDate: application.applicationDate,
        responseDate: application.responseDate,
        nextFollowUpDate: application.nextFollowUpDate,
        status: application.status,
        workMode: application.workMode,
        salary: application.salary,
        jobUrl: application.jobUrl,
        jobDetails: application.jobDetails,
        notes: application.notes,
        removeResume: removed.has("resume"),
        removeCoverLetter: removed.has("coverLetter")
    };
    for (const [key, value] of Object.entries(fields)) {
        formData.append(key, value == null ? "" : String(value));
    }
    for (const field of fileFields) {
        const selectedFile = document.getElementById(field.inputId)?.files[0];
        const legacyFile = application[field.key];
        if (selectedFile) {
            formData.append(field.key, selectedFile, selectedFile.name);
        } else if (legacyFile && legacyFile.blob instanceof Blob) {
            formData.append(field.key, legacyFile.blob, legacyFile.name);
        }
    }
    return formData;
}

async function migrateLegacyApplications() {
    if (localStorage.getItem(MIGRATION_KEY) === "complete") return;
    const legacyDatabase = await openLegacyDatabase();
    try {
        const legacyApplications = await getLegacyApplications(legacyDatabase);
        const serverApplications = await apiRequest("/api/applications");
        const serverIds = new Set(serverApplications.map(application => application.id.toLowerCase()));
        for (const application of legacyApplications) {
            if (serverIds.has(application.id.toLowerCase())) continue;
            await apiRequest("/api/applications", {
                method: "POST",
                body: applicationFormData(application, application.id)
            });
        }
        localStorage.setItem(MIGRATION_KEY, "complete");
    } finally {
        legacyDatabase.close();
    }
}

function escapeHtml(value = "") {
    return String(value).replace(/[&<>"']/g, character => ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        '"': "&quot;",
        "'": "&#039;"
    })[character]);
}

function formatDate(value) {
    if (!value) return "";
    const [year, month, day] = value.split("-").map(Number);
    return new Intl.DateTimeFormat("es-AR", { day: "2-digit", month: "short", year: "numeric" })
        .format(new Date(year, month - 1, day));
}

function getLocalDate() {
    const now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    return now.toISOString().slice(0, 10);
}

function dateKey(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

function applicationDate(application) {
    const [year, month, day] = application.applicationDate.split("-").map(Number);
    return new Date(year, month - 1, day);
}

function getActivityBuckets() {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const buckets = [];

    if (activityPeriod === "day") {
        for (let offset = 6; offset >= 0; offset--) {
            const date = new Date(today);
            date.setDate(date.getDate() - offset);
            buckets.push({
                key: dateKey(date),
                label: new Intl.DateTimeFormat("es-AR", { day: "numeric", month: "short" }).format(date),
                title: new Intl.DateTimeFormat("es-AR", { dateStyle: "long" }).format(date)
            });
        }
    } else if (activityPeriod === "week") {
        const mondayOffset = (today.getDay() + 6) % 7;
        const currentMonday = new Date(today);
        currentMonday.setDate(today.getDate() - mondayOffset);
        for (let offset = 7; offset >= 0; offset--) {
            const start = new Date(currentMonday);
            start.setDate(currentMonday.getDate() - offset * 7);
            const end = new Date(start);
            end.setDate(start.getDate() + 6);
            buckets.push({
                key: dateKey(start),
                label: `${new Intl.DateTimeFormat("es-AR", { day: "numeric", month: "short" }).format(start)}–${new Intl.DateTimeFormat("es-AR", { day: "numeric", month: "short" }).format(end)}`,
                title: `Semana del ${new Intl.DateTimeFormat("es-AR", { dateStyle: "long" }).format(start)}`
            });
        }
    } else {
        const currentMonth = new Date(today.getFullYear(), today.getMonth(), 1);
        for (let offset = 5; offset >= 0; offset--) {
            const date = new Date(currentMonth.getFullYear(), currentMonth.getMonth() - offset, 1);
            buckets.push({
                key: `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`,
                label: new Intl.DateTimeFormat("es-AR", { month: "short" }).format(date),
                title: new Intl.DateTimeFormat("es-AR", { month: "long", year: "numeric" }).format(date)
            });
        }
    }

    const counts = new Map(buckets.map(bucket => [bucket.key, 0]));
    for (const application of applications) {
        const date = applicationDate(application);
        const key = activityPeriod === "day"
            ? dateKey(date)
            : activityPeriod === "week"
                ? dateKey(new Date(date.getFullYear(), date.getMonth(), date.getDate() - ((date.getDay() + 6) % 7)))
                : `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;
        if (counts.has(key)) counts.set(key, counts.get(key) + 1);
    }
    return buckets.map(bucket => ({ ...bucket, count: counts.get(bucket.key) }));
}

function renderActivity() {
    const buckets = getActivityBuckets();
    const total = buckets.reduce((sum, bucket) => sum + bucket.count, 0);
    const maximum = Math.max(1, ...buckets.map(bucket => bucket.count));
    const periodDetails = {
        day: { title: "Postulaciones por día", description: "Últimos 7 días", summary: "postulaciones enviadas en los últimos 7 días" },
        week: { title: "Postulaciones por semana", description: "Últimas 8 semanas · de lunes a domingo", summary: "postulaciones enviadas en las últimas 8 semanas" },
        month: { title: "Postulaciones por mes", description: "Últimos 6 meses", summary: "postulaciones enviadas en los últimos 6 meses" }
    }[activityPeriod];

    document.getElementById("activityTitle").textContent = periodDetails.title;
    document.getElementById("activityDescription").textContent = periodDetails.description;
    document.getElementById("periodTotal").textContent = total;
    document.getElementById("periodSummary").textContent = periodDetails.summary;
    const chart = document.getElementById("activityChart");
    chart.dataset.period = activityPeriod;
    chart.innerHTML = buckets.map(bucket => {
        const height = bucket.count ? Math.max(10, Math.round(bucket.count / maximum * 100)) : 0;
        return `<div class="chart-column" title="${escapeHtml(bucket.title)}: ${bucket.count}">
            <span class="bar-count">${bucket.count}</span>
            <div class="bar-track"><span class="bar-fill" style="height: ${height}%"></span></div>
            <span class="bar-label">${escapeHtml(bucket.label)}</span>
        </div>`;
    }).join("");
}

function getDateDifference(dateValue) {
    const [year, month, day] = dateValue.split("-").map(Number);
    const due = Date.UTC(year, month - 1, day);
    const [currentYear, currentMonth, currentDay] = getLocalDate().split("-").map(Number);
    const today = Date.UTC(currentYear, currentMonth - 1, currentDay);
    return Math.round((due - today) / 86400000);
}

function renderFollowUps() {
    const upcomingLimit = new Date();
    upcomingLimit.setDate(upcomingLimit.getDate() + 7);
    const limitKey = dateKey(upcomingLimit);
    const followUps = applications
        .filter(application => application.nextFollowUpDate)
        .filter(application => !["Rechazada", "Retirada"].includes(application.status))
        .filter(application => application.nextFollowUpDate <= limitKey)
        .sort((first, second) => first.nextFollowUpDate.localeCompare(second.nextFollowUpDate));
    const overdueCount = followUps.filter(application => getDateDifference(application.nextFollowUpDate) < 0).length;
    const todayCount = followUps.filter(application => getDateDifference(application.nextFollowUpDate) === 0).length;
    const upcomingCount = followUps.length - overdueCount - todayCount;
    const notice = document.getElementById("followUpNotice");

    document.getElementById("followUpCount").textContent = followUps.length;
    if (overdueCount || todayCount) {
        const messages = [];
        if (overdueCount) messages.push(`${overdueCount} vencido${overdueCount === 1 ? "" : "s"}`);
        if (todayCount) messages.push(`${todayCount} para hoy`);
        notice.textContent = `Tenés seguimientos pendientes: ${messages.join(" y ")}.`;
        notice.className = "follow-up-notice urgent";
        notice.hidden = false;
    } else if (upcomingCount) {
        notice.textContent = `Tenés ${upcomingCount} seguimiento${upcomingCount === 1 ? "" : "s"} próximo${upcomingCount === 1 ? "" : "s"} en los próximos 7 días.`;
        notice.className = "follow-up-notice";
        notice.hidden = false;
    } else {
        notice.hidden = true;
    }

    document.getElementById("followUpEmpty").hidden = followUps.length > 0;
    document.getElementById("followUpList").innerHTML = followUps.map(application => {
        const daysUntil = getDateDifference(application.nextFollowUpDate);
        const dateLabel = daysUntil < 0
            ? `Vencido hace ${Math.abs(daysUntil)} día${Math.abs(daysUntil) === 1 ? "" : "s"}`
            : daysUntil === 0
                ? "Para hoy"
                : daysUntil === 1
                    ? "Mañana"
                    : `En ${daysUntil} días`;
        const urgencyClass = daysUntil <= 0 ? "due" : "upcoming";
        return `<article class="follow-up-item">
            <div class="follow-up-date ${urgencyClass}">
                <strong>${escapeHtml(formatDate(application.nextFollowUpDate))}</strong>
                <span>${dateLabel}</span>
            </div>
            <div class="follow-up-job">
                <strong>${escapeHtml(application.company)}</strong>
                <span>${escapeHtml(application.position)} · ${escapeHtml(application.status)}</span>
            </div>
            <div class="follow-up-actions">
                <button class="action-button" type="button" data-action="edit" data-id="${escapeHtml(application.id)}" ${isReadOnlyFallback ? "disabled" : ""}>Editar</button>
                <button class="action-button complete-follow-up" type="button" data-action="complete-follow-up" data-id="${escapeHtml(application.id)}" ${isReadOnlyFallback ? "disabled" : ""}>Marcar hecho</button>
            </div>
        </article>`;
    }).join("");
}

function renderInterviews() {
    const interviewList = document.getElementById("interviewList");
    const sortedInterviews = [...interviews].sort((first, second) =>
        first.scheduledUtc.localeCompare(second.scheduledUtc));
    document.getElementById("interviewCount").textContent = sortedInterviews.length;
    document.getElementById("interviewEmpty").hidden = sortedInterviews.length > 0;
    interviewList.innerHTML = sortedInterviews.map(interview => {
        const scheduled = new Intl.DateTimeFormat("es-AR", {
            dateStyle: "medium",
            timeStyle: "short"
        }).format(new Date(interview.scheduledUtc));
        const details = [interview.interviewType, interview.locationOrLink, interview.contactName]
            .filter(Boolean).map(escapeHtml).join(" · ");
        return `<article class="interview-item">
            <div class="interview-scheduled">
                <strong>${escapeHtml(scheduled)}</strong>
                <span>${escapeHtml(interview.company)} · ${escapeHtml(interview.position)}</span>
                ${details ? `<span>${details}</span>` : ""}
                ${interview.notes ? `<span>${escapeHtml(interview.notes)}</span>` : ""}
            </div>
            <div class="interview-actions">
                <button class="action-button" type="button" data-action="edit-interview" data-id="${escapeHtml(interview.id)}" ${isReadOnlyFallback ? "disabled" : ""}>Editar</button>
                <button class="action-button delete" type="button" data-action="delete-interview" data-id="${escapeHtml(interview.id)}" ${isReadOnlyFallback ? "disabled" : ""}>Eliminar</button>
            </div>
        </article>`;
    }).join("");
}

function statusClass(status) {
    return `status-${status.toLowerCase().replaceAll(" ", "-")}`;
}

function fileLink(file, label) {
    if (!file) return "";
    if (file.downloadUrl) {
        return `<a class="document-link" href="${escapeHtml(file.downloadUrl)}" download>${label}</a>`;
    }
    if (!(file.blob instanceof Blob)) return "";
    const url = URL.createObjectURL(file.blob);
    objectUrls.push(url);
    return `<a class="document-link" href="${url}" download="${escapeHtml(file.name)}">${label}</a>`;
}

function render() {
    objectUrls.forEach(url => URL.revokeObjectURL(url));
    objectUrls = [];

    document.getElementById("totalApplications").textContent = applications.length;
    document.getElementById("pendingApplications").textContent = applications
        .filter(application => ["Postulada", "En revisión"].includes(application.status)).length;
    document.getElementById("interviewApplications").textContent = applications
        .filter(application => application.status === "Entrevista").length;
    document.getElementById("offerApplications").textContent = applications
        .filter(application => application.status === "Oferta").length;
    renderActivity();
    renderFollowUps();
    renderInterviews();

    const query = elements.search.value.trim().toLocaleLowerCase("es");
    const selectedStatus = elements.statusFilter.value;
    const filteredApplications = applications
        .filter(application => !selectedStatus || application.status === selectedStatus)
        .filter(application => !query || [
            application.company, application.companyDetails, application.position, application.workMode,
            application.jobDetails, application.notes
        ].some(value => value && value.toLocaleLowerCase("es").includes(query)))
        .sort((first, second) => second.applicationDate.localeCompare(first.applicationDate));

    elements.table.innerHTML = filteredApplications.map(application => {
        const documents = [
            fileLink(application.resume, "CV"),
            fileLink(application.coverLetter, "Carta")
        ].filter(Boolean);
        const responseDate = application.responseDate
            ? `<span class="position-name">Respuesta: ${escapeHtml(formatDate(application.responseDate))}</span>`
            : "";
        const extraInfo = [
            application.companyDetails,
            application.jobDetails,
            application.workMode,
            application.salary,
            application.notes
        ].filter(Boolean).map(value => escapeHtml(value.length > 110 ? `${value.slice(0, 107)}...` : value)).join(" · ");
        const jobLink = application.jobUrl
            ? ` · <a class="document-link" href="${escapeHtml(application.jobUrl)}" target="_blank" rel="noopener noreferrer">Ver búsqueda</a>`
            : "";
        const companyName = application.companyWebsiteUrl
            ? `<a class="company-name company-link" href="${escapeHtml(application.companyWebsiteUrl)}" target="_blank" rel="noopener noreferrer">${escapeHtml(application.company)}</a>`
            : `<span class="company-name">${escapeHtml(application.company)}</span>`;

        return `<tr>
            <td>
                ${companyName}
                <span class="position-name">${escapeHtml(application.position)}</span>
                ${extraInfo ? `<span class="position-name">${extraInfo}</span>` : ""}
                ${jobLink ? `<span class="position-name">${jobLink}</span>` : ""}
            </td>
            <td>${escapeHtml(formatDate(application.applicationDate))}${responseDate}</td>
            <td><span class="status-badge ${statusClass(application.status)}">${escapeHtml(application.status)}</span></td>
            <td><div class="document-links">${documents.length ? documents.join("") : '<span class="position-name">Sin archivos</span>'}</div></td>
            <td>
                <div class="row-actions">
                    <button class="action-button" type="button" data-action="history" data-id="${escapeHtml(application.id)}" aria-label="Ver historial de ${escapeHtml(application.position)}" ${isReadOnlyFallback ? "disabled" : ""}>Historial</button>
                    <button class="action-button" type="button" data-action="schedule-interview" data-id="${escapeHtml(application.id)}" aria-label="Agendar entrevista para ${escapeHtml(application.position)}" ${isReadOnlyFallback ? "disabled" : ""}>Entrevista</button>
                    <button class="action-button" type="button" data-action="edit" data-id="${escapeHtml(application.id)}" aria-label="Editar ${escapeHtml(application.position)}" ${isReadOnlyFallback ? "disabled" : ""}>Editar</button>
                    <button class="action-button delete" type="button" data-action="delete" data-id="${escapeHtml(application.id)}" aria-label="Eliminar ${escapeHtml(application.position)}" ${isReadOnlyFallback ? "disabled" : ""}>Eliminar</button>
                </div>
            </td>
        </tr>`;
    }).join("");

    if (filteredApplications.length) {
        elements.emptyState.hidden = true;
    } else {
        elements.emptyState.hidden = false;
        const hasApplications = applications.length > 0;
        elements.emptyState.querySelector("h3").textContent = hasApplications
            ? "No encontramos postulaciones"
            : "Tu próxima oportunidad empieza acá";
        elements.emptyState.querySelector("p").textContent = hasApplications
            ? "Probá cambiar la búsqueda o el filtro de estado."
            : "Registrá tu primera postulación y llevá un seguimiento ordenado de cada proceso.";
        document.getElementById("emptyAddButton").hidden = hasApplications;
    }
}

function setFormError(message) {
    elements.formError.textContent = message;
    elements.formError.hidden = !message;
}

function showCurrentFiles(application) {
    for (const field of fileFields) {
        const file = application && application[field.key];
        const container = document.getElementById(field.currentId);
        container.replaceChildren();
        if (!file) continue;

        const name = document.createElement("span");
        name.textContent = `Archivo actual: ${file.name} `;
        const removeButton = document.createElement("button");
        removeButton.type = "button";
        removeButton.className = "document-link remove-file";
        removeButton.textContent = "Quitar";
        removeButton.dataset.removeFile = field.key;
        container.append(name, removeButton);
    }
}

function resetForm() {
    elements.form.reset();
    document.getElementById("applicationId").value = "";
    document.getElementById("applicationDate").value = getLocalDate();
    elements.formTitle.textContent = "Registrar postulación";
    elements.saveButton.textContent = "Guardar postulación";
    removedFiles = new Set();
    showCurrentFiles(null);
    setFormError("");
}

function openForm(application = null) {
    resetForm();
    if (application) {
        document.getElementById("applicationId").value = application.id;
        document.getElementById("company").value = application.company;
        document.getElementById("companyDetails").value = application.companyDetails || "";
        document.getElementById("companyWebsiteUrl").value = application.companyWebsiteUrl || "";
        document.getElementById("position").value = application.position;
        document.getElementById("applicationDate").value = application.applicationDate;
        document.getElementById("status").value = application.status;
        document.getElementById("responseDate").value = application.responseDate || "";
        document.getElementById("nextFollowUpDate").value = application.nextFollowUpDate || "";
        document.getElementById("workMode").value = application.workMode || "";
        document.getElementById("salary").value = application.salary || "";
        document.getElementById("jobUrl").value = application.jobUrl || "";
        document.getElementById("jobDetails").value = application.jobDetails || "";
        document.getElementById("notes").value = application.notes || "";
        elements.formTitle.textContent = "Editar postulación";
        elements.saveButton.textContent = "Guardar cambios";
        showCurrentFiles(application);
    }
    elements.formSection.hidden = false;
    elements.formSection.scrollIntoView({ behavior: "smooth", block: "start" });
    document.getElementById("company").focus({ preventScroll: true });
}

function getSelectedFile(inputId) {
    return document.getElementById(inputId).files[0] || null;
}

function validateFiles() {
    for (const field of fileFields) {
        const file = getSelectedFile(field.inputId);
        if (!file) continue;
        const extension = file.name.split(".").pop().toLowerCase();
        if (!allowedExtensions.has(extension)) {
            return `El archivo de ${field.label} debe ser PDF, DOC, DOCX u ODT.`;
        }
        if (file.size > MAX_FILE_SIZE) {
            return `El archivo de ${field.label} supera el máximo de 10 MB.`;
        }
    }
    return "";
}

async function submitForm(event) {
    event.preventDefault();
    setFormError("");
    const jobUrl = document.getElementById("jobUrl").value.trim();
    if (jobUrl) {
        try {
            const parsedUrl = new URL(jobUrl);
            if (!["http:", "https:"].includes(parsedUrl.protocol)) {
                setFormError("El enlace de la búsqueda debe comenzar con http:// o https://.");
                return;
            }
        } catch {
            setFormError("Ingresá un enlace válido para la búsqueda.");
            return;
        }
    }

    const fileError = validateFiles();
    if (fileError) {
        setFormError(fileError);
        return;
    }

    elements.saveButton.disabled = true;
    try {
        const id = document.getElementById("applicationId").value;
        const previous = applications.find(application => application.id === id);
        const application = {
            company: document.getElementById("company").value.trim(),
            companyDetails: document.getElementById("companyDetails").value.trim(),
            companyWebsiteUrl: document.getElementById("companyWebsiteUrl").value.trim(),
            position: document.getElementById("position").value.trim(),
            applicationDate: document.getElementById("applicationDate").value,
            status: document.getElementById("status").value,
            responseDate: document.getElementById("responseDate").value,
            nextFollowUpDate: ["Rechazada", "Retirada"].includes(document.getElementById("status").value)
                ? ""
                : document.getElementById("nextFollowUpDate").value,
            workMode: document.getElementById("workMode").value.trim(),
            salary: document.getElementById("salary").value.trim(),
            jobUrl,
            jobDetails: document.getElementById("jobDetails").value.trim(),
            notes: document.getElementById("notes").value.trim(),
            resume: previous && !removedFiles.has("resume") ? previous.resume : null,
            coverLetter: previous && !removedFiles.has("coverLetter") ? previous.coverLetter : null
        };

        for (const field of fileFields) {
            const file = getSelectedFile(field.inputId);
            if (file) {
                application[field.key] = { name: file.name, type: file.type, size: file.size, blob: file };
            }
        }

        const method = id ? "PUT" : "POST";
        const url = id ? `/api/applications/${encodeURIComponent(id)}` : "/api/applications";
        await apiRequest(url, {
        method,
        body: applicationFormData(application, id, removedFiles)
        });
        applications = await getApplications();
        render();
        elements.formSection.hidden = true;
        resetForm();
    } catch (error) {
        console.error("Error al guardar la postulación:", error);
        setFormError(error.message || "No se pudo guardar la postulación. Intentá nuevamente.");
    } finally {
        elements.saveButton.disabled = false;
    }
}

async function handleTableAction(event) {
    const button = event.target.closest("button[data-action]");
    if (!button) return;
    const application = applications.find(item => item.id === button.dataset.id);
    if (!application) return;

    if (button.dataset.action === "edit") {
        openForm(application);
        return;
    }

    if (button.dataset.action === "history") {
        await openHistory(application);
        return;
    }

    if (button.dataset.action === "schedule-interview") {
        openInterview(application);
        return;
    }

    if (button.dataset.action === "complete-follow-up") {
        try {
            await apiRequest(`/api/applications/${encodeURIComponent(application.id)}/reminders/complete`, {
                method: "POST"
            });
            applications = await getApplications();
            render();
        } catch (error) {
            console.error("Error al completar el seguimiento:", error);
            alert("No se pudo actualizar el seguimiento. Intentá nuevamente.");
        }
        return;
    }

    if (button.dataset.action === "delete" && confirm(`¿Querés eliminar la postulación de ${application.position} en ${application.company}?`)) {
        try {
            await apiRequest(`/api/applications/${encodeURIComponent(application.id)}`, { method: "DELETE" });
            applications = await getApplications();
            render();
        } catch (error) {
            console.error("Error al eliminar la postulación:", error);
            alert("No se pudo eliminar la postulación. Intentá nuevamente.");
        }
    }
}

function openInterview(application, interview = null) {
    const form = document.getElementById("interviewForm");
    form.reset();
    const interviewIdInput = document.getElementById("interviewId");
    const applicationIdInput = document.getElementById("interviewApplicationId");
    interviewIdInput.value = interview ? interview.id : "";
    applicationIdInput.value = interview ? interview.applicationId : application.id;
    const selectedApplication = application || applications.find(item => item.id === interview.applicationId);
    document.getElementById("interviewSubtitle").textContent =
        `${selectedApplication.company} · ${selectedApplication.position}`;
    document.getElementById("interviewDialogTitle").textContent =
        interview ? "Editar entrevista" : "Agendar entrevista";
    document.getElementById("saveInterviewButton").textContent =
        interview ? "Guardar cambios" : "Guardar entrevista";
    document.getElementById("interviewError").hidden = true;

    if (interview) {
        const localDate = new Date(new Date(interview.scheduledUtc).getTime() -
            new Date(interview.scheduledUtc).getTimezoneOffset() * 60000);
        document.getElementById("interviewScheduledAt").value = localDate.toISOString().slice(0, 16);
        document.getElementById("interviewType").value = interview.interviewType || "";
        document.getElementById("interviewLocation").value = interview.locationOrLink || "";
        document.getElementById("interviewContact").value = interview.contactName || "";
        document.getElementById("interviewNotes").value = interview.notes || "";
    }
    document.getElementById("interviewDialog").showModal();
}

async function submitInterview(event) {
    event.preventDefault();
    const saveButton = document.getElementById("saveInterviewButton");
    const errorElement = document.getElementById("interviewError");
    const interviewId = document.getElementById("interviewId").value;
    const applicationId = document.getElementById("interviewApplicationId").value;
    saveButton.disabled = true;
    errorElement.hidden = true;
    try {
        const payload = {
            scheduledAt: new Date(document.getElementById("interviewScheduledAt").value).toISOString(),
            interviewType: document.getElementById("interviewType").value.trim(),
            locationOrLink: document.getElementById("interviewLocation").value.trim(),
            contactName: document.getElementById("interviewContact").value.trim(),
            notes: document.getElementById("interviewNotes").value.trim()
        };
        await apiRequest(interviewId
            ? `/api/interviews/${encodeURIComponent(interviewId)}`
            : `/api/applications/${encodeURIComponent(applicationId)}/interviews`, {
            method: interviewId ? "PUT" : "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });
        await getInterviews();
        render();
        document.getElementById("interviewDialog").close();
    } catch (error) {
        console.error("No se pudo guardar la entrevista:", error);
        errorElement.textContent = error.message || "No se pudo guardar la entrevista.";
        errorElement.hidden = false;
    } finally {
        saveButton.disabled = false;
    }
}

async function handleInterviewAction(event) {
    const button = event.target.closest("button[data-action]");
    if (!button) return;
    const interview = interviews.find(item => item.id === button.dataset.id);
    if (!interview) return;
    if (button.dataset.action === "edit-interview") {
        openInterview(null, interview);
        return;
    }
    if (button.dataset.action === "delete-interview" &&
        confirm(`¿Querés eliminar la entrevista de ${interview.company} para ${interview.position}?`)) {
        try {
            await apiRequest(`/api/interviews/${encodeURIComponent(interview.id)}`, { method: "DELETE" });
            await getInterviews();
            render();
        } catch (error) {
            console.error("No se pudo eliminar la entrevista:", error);
            alert(error.message || "No se pudo eliminar la entrevista.");
        }
    }
}

async function openHistory(application) {
    historyApplicationId = application.id;
    document.getElementById("historySubtitle").textContent = `${application.company} · ${application.position}`;
    document.getElementById("historyNote").value = "";
    document.getElementById("historyError").hidden = true;
    document.getElementById("historyEntries").innerHTML = "<p class=\"history-loading\">Cargando historial…</p>";
    document.getElementById("historyDialog").showModal();
    try {
        const entries = await apiRequest(`/api/applications/${encodeURIComponent(application.id)}/history`);
        document.getElementById("historyEntries").innerHTML = entries.length
            ? entries.map(entry => `<article class="history-entry">
                <time datetime="${escapeHtml(entry.occurredUtc)}">${escapeHtml(new Intl.DateTimeFormat("es-AR", {
                    dateStyle: "medium",
                    timeStyle: "short"
                }).format(new Date(entry.occurredUtc)))}</time>
                <p>${escapeHtml(entry.description)}</p>
            </article>`).join("")
            : "<p class=\"history-loading\">Todavía no hay movimientos.</p>";
    } catch (error) {
        console.error("No se pudo cargar el historial:", error);
        const historyError = document.getElementById("historyError");
        historyError.textContent = error.message || "No se pudo cargar el historial.";
        historyError.hidden = false;
        document.getElementById("historyEntries").replaceChildren();
    }
}

async function addHistoryEntry(event) {
    event.preventDefault();
    const saveButton = document.getElementById("saveHistoryButton");
    const errorElement = document.getElementById("historyError");
    saveButton.disabled = true;
    errorElement.hidden = true;
    try {
        await apiRequest(`/api/applications/${encodeURIComponent(historyApplicationId)}/history`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ description: document.getElementById("historyNote").value.trim() })
        });
        const application = applications.find(item => item.id === historyApplicationId);
        if (application) await openHistory(application);
        document.getElementById("historyNote").value = "";
    } catch (error) {
        console.error("No se pudo guardar la nota:", error);
        errorElement.textContent = error.message || "No se pudo guardar la nota.";
        errorElement.hidden = false;
    } finally {
        saveButton.disabled = false;
    }
}

async function submitLogin(event) {
    event.preventDefault();
    const button = document.getElementById("loginButton");
    const errorElement = document.getElementById("loginError");
    button.disabled = true;
    errorElement.hidden = true;
    try {
        const account = await apiRequest("/api/auth/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                email: document.getElementById("loginEmail").value,
                password: document.getElementById("loginPassword").value
            })
        });
        await refreshCsrfToken();
        document.getElementById("loginForm").reset();
        await loadAuthenticatedApp(account.email);
    } catch (error) {
        errorElement.textContent = error.status === 401
            ? "El correo o la contraseña no son correctos, o la cuenta está temporalmente bloqueada."
            : error.message || "No se pudo iniciar sesión.";
        errorElement.hidden = false;
    } finally {
        button.disabled = false;
    }
}

async function submitSetup(event) {
    event.preventDefault();
    const button = document.getElementById("setupButton");
    const errorElement = document.getElementById("setupError");
    const password = document.getElementById("setupPassword").value;
    if (password !== document.getElementById("setupPasswordConfirmation").value) {
        errorElement.textContent = "Las contraseñas no coinciden.";
        errorElement.hidden = false;
        return;
    }

    button.disabled = true;
    errorElement.hidden = true;
    try {
        const account = await apiRequest("/api/auth/setup", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                setupKey: document.getElementById("setupKey").value,
                displayName: document.getElementById("setupDisplayName").value,
                email: document.getElementById("setupEmail").value,
                password
            })
        });
        await refreshCsrfToken();
        document.getElementById("setupForm").reset();
        await loadAuthenticatedApp(account.email);
    } catch (error) {
        errorElement.textContent = error.status === 401
            ? "El código inicial no es válido. Copiá el código vigente de la consola del backend."
            : error.message || "No se pudo crear la cuenta.";
        errorElement.hidden = false;
    } finally {
        button.disabled = false;
    }
}

async function logout() {
    const button = document.getElementById("logoutButton");
    button.disabled = true;
    try {
        await apiRequest("/api/auth/logout", { method: "POST" });
        csrfToken = "";
        await refreshCsrfToken();
        showAuthentication("login", "Cerraste sesión correctamente.");
        document.getElementById("loginForm").reset();
        applications = [];
        interviews = [];
    } catch (error) {
        document.getElementById("connectionStatus").textContent = error instanceof TypeError
            ? "No se pudo contactar con el servidor. Para cerrar la sesión, mantené JobTrack abierto desde la dirección que muestra «dotnet run» y volvé a intentar."
            : error.message || "No se pudo cerrar la sesión.";
        document.getElementById("connectionStatus").className = "connection-status disconnected";
        document.getElementById("connectionStatus").hidden = false;
    } finally {
        button.disabled = false;
    }
}

function closeForm() {
    elements.formSection.hidden = true;
    resetForm();
}

document.getElementById("addJobButton").addEventListener("click", () => openForm());
document.getElementById("logoutButton").addEventListener("click", logout);
document.getElementById("loginForm").addEventListener("submit", submitLogin);
document.getElementById("setupForm").addEventListener("submit", submitSetup);
document.getElementById("addJobButtonSecondary").addEventListener("click", () => openForm());
document.getElementById("emptyAddButton").addEventListener("click", () => openForm());
document.getElementById("cancelButton").addEventListener("click", closeForm);
document.getElementById("closeFormButton").addEventListener("click", closeForm);
document.getElementById("closeHistoryButton").addEventListener("click", () => document.getElementById("historyDialog").close());
document.getElementById("cancelHistoryButton").addEventListener("click", () => document.getElementById("historyDialog").close());
document.getElementById("historyForm").addEventListener("submit", addHistoryEntry);
document.getElementById("closeInterviewButton").addEventListener("click", () => document.getElementById("interviewDialog").close());
document.getElementById("cancelInterviewButton").addEventListener("click", () => document.getElementById("interviewDialog").close());
document.getElementById("interviewForm").addEventListener("submit", submitInterview);
document.getElementById("interviewList").addEventListener("click", handleInterviewAction);
elements.form.addEventListener("submit", submitForm);
elements.table.addEventListener("click", handleTableAction);
document.getElementById("followUpList").addEventListener("click", handleTableAction);
elements.search.addEventListener("input", render);
elements.statusFilter.addEventListener("change", render);
document.querySelector(".period-switch").addEventListener("click", event => {
    const button = event.target.closest("button[data-period]");
    if (!button || button.dataset.period === activityPeriod) return;
    activityPeriod = button.dataset.period;
    document.querySelectorAll(".period-button").forEach(periodButton => {
        const isActive = periodButton === button;
        periodButton.classList.toggle("active", isActive);
        periodButton.setAttribute("aria-pressed", String(isActive));
    });
    renderActivity();
});

elements.formSection.addEventListener("click", event => {
    const button = event.target.closest("button[data-remove-file]");
    if (!button) return;
    removedFiles.add(button.dataset.removeFile);
    button.parentElement.textContent = "Se quitará al guardar los cambios.";
});

async function initialize() {
    try {
        await refreshCsrfToken();
        const status = await apiRequest("/api/auth/status");
        if (status.setupRequired) {
            showAuthentication("setup");
            return;
        }

        try {
            const account = await apiRequest("/api/auth/me");
            await loadAuthenticatedApp(account.email);
        } catch (error) {
            if (error.status !== 401) throw error;
            showAuthentication("login");
        }
    } catch (error) {
        console.error("No se pudo iniciar JobTrack:", error);
        showAuthentication("login", `No se pudo conectar con el backend. Iniciá JobTrack con dotnet run e ingresá desde la dirección que muestra la consola. Detalle: ${error.message}`);
    }
}

initialize();
