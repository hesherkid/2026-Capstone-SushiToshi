import CustomerPageContainer from "@/components/customer/CustomerPageContainer";
import ServiceRequestForm from "@/components/customer/OrderDashboard/components/ServiceRequestForm";

const CallServerPage = () => {
  return (
    <CustomerPageContainer>
      <ServiceRequestForm />
    </CustomerPageContainer>
  );
};

export default CallServerPage;
