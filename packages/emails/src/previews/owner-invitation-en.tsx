import { Invitation } from "../templates/invitation";
import { ownerInvitation } from "./samples";

export default function Preview() {
  return <Invitation {...ownerInvitation} />;
}
