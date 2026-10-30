import { Notification } from "../templates/notification";
import { notification } from "./samples";

export default function Preview() {
  return <Notification {...notification.en} />;
}
