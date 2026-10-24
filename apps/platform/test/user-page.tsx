import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { UserPage } from "@/features/users/user-page";
import { renderWithProviders } from "./render";

/** Renders a user's page and opens an action of the header's menu; returns the dialog that opens. */
export async function openUserAction(
  user: { id: string },
  name: string,
  action: string,
  dialog: string,
) {
  renderWithProviders(<UserPage userId={user.id} />);
  await userEvent.click(
    await screen.findByRole("button", { name: `Actions for ${name}` }),
  );
  await userEvent.click(await screen.findByRole("menuitem", { name: action }));
  return within(await screen.findByRole("alertdialog", { name: dialog }));
}

/** Renders a user's page and opens Make Owner on one membership; returns the dialog that opens. */
export async function openMakeOwner(
  user: { id: string },
  organisation: string,
  dialog: string,
) {
  renderWithProviders(<UserPage userId={user.id} />);
  await userEvent.click(
    await screen.findByRole("button", { name: `Actions for ${organisation}` }),
  );
  await userEvent.click(
    await screen.findByRole("menuitem", { name: "Make Owner" }),
  );
  return within(await screen.findByRole("alertdialog", { name: dialog }));
}
