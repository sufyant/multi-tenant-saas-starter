/** Settings › System admins, where a user becomes a system admin or stops being one. */
export const systemAdminsPage = "/settings/system-admins";

/** A system admin's page, which a phone card opens (10.14, 10.15). */
export const systemAdminPage = (id: string) => `${systemAdminsPage}/${id}`;
