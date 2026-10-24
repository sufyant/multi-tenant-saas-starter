import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { OrganisationPage } from "@/features/organisations/organisation-page";
import { renderWithProviders } from "./render";

/** Renders an organisation's page and opens an action of its menu; returns the dialog that opens. */
export async function openAction(
  organisation: { id: string; name: string },
  action: string,
  dialog: string,
) {
  renderWithProviders(<OrganisationPage tenantId={organisation.id} />);
  await userEvent.click(
    await screen.findByRole("button", {
      name: `Actions for ${organisation.name}`,
    }),
  );
  await userEvent.click(await screen.findByRole("menuitem", { name: action }));
  return within(await screen.findByRole("alertdialog", { name: dialog }));
}
