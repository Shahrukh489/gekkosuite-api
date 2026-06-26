# Database Schema

## Auth / RBAC
- User(user_id, email, name, phone, password_hash)
- User_Role(user_id, role_id, resource_type, resource_id)   -- key (user_id, role_id); resource_type: ORG | STORE; resource_id = the org_id or store_id this grant applies to (the "where"); never null. (resource_type, resource_id) is a polymorphic reference — integrity enforced in the app / by a trigger, not a single DB foreign key (see "Alternate design" in architecture.md)
- Role(role_id, name, organization_id, is_managed, description)   -- is_managed=true: global, we manage it (organization_id null); is_managed=false: custom, owned by an organization (custom roles are org-level only); unique (name, organization_id)
- Permission(permission_id, name, subject, action)
- Role_Permission(role_id, permission_id)

## Plans / Features
- Plan(plan_id, name, description)
- Feature(feature_id, name, description)
- Plan_Feature(plan_id, feature_id)

## Organization
- Organization(organization_id, plan_id, name, owner_user_id, description, country, city, state, zip_code)

## Store
- Store(store_id, organization_id, name, manager, description, country, region, city, state, zip_code)
- Store_Product(store_product_id, store_id, name, description, sku, price, quantity)
- Store_Customer(store_customer_id, store_id, name, email, number)
- Store_Order(store_order_id, store_id, store_customer_id, total, date)
- Store_Order_Product(store_order_id, store_product_id, quantity)
- Store_Return(store_return_id, store_id, store_order_id)
- Store_Return_Product(store_return_id, store_product_id, quantity)
