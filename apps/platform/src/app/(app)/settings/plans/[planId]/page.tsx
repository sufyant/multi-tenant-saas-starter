import { PlanEditor } from "@/features/plans/plan-editor";

export default async function PlatformPlanPage({
  params,
}: PageProps<"/settings/plans/[planId]">) {
  const { planId } = await params;
  return <PlanEditor planId={planId} />;
}
