import { AccountEmail } from "../templates/account";
import { account } from "./samples";

export default function Preview() {
  return <AccountEmail {...account.emailChangeCode} />;
}
