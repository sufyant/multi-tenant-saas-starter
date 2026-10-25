import { Invitation } from "../templates/invitation";
import { invitation } from "./samples";

export default function Preview() {
  return <Invitation {...invitation.en} />;
}
