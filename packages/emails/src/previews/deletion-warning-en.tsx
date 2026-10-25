import { DeletionWarning } from "../templates/deletion-warning";
import { deletionWarning } from "./samples";

export default function Preview() {
  return <DeletionWarning {...deletionWarning.en} />;
}
