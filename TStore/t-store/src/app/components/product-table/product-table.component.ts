import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';

import { NzMessageService } from 'ng-zorro-antd/message';

import { ProductModel } from 'src/app/models/product.model';

import { ProductService } from 'src/app/services/product.service';
import { InteractionService } from 'src/app/services/interaction.service';
import { OrderService } from 'src/app/services/order.service';

@Component({
  selector: 'app-product-table',
  templateUrl: './product-table.component.html',
  styleUrls: ['./product-table.component.scss']
})
export class ProductTableComponent implements OnInit {

  searchValue: string;
  productSelections: boolean[];
  selectedProducts: ProductModel[];
  loading: boolean;
  products?: ProductModel[];
  originalProducts?: ProductModel[];

  constructor(private _productService: ProductService,
    private _interactionService: InteractionService,
    private _orderService: OrderService,
    private _messageService: NzMessageService,
    private _router: Router) {
    this.searchValue = '';
    this.productSelections = [];
    this.selectedProducts = [];
    this.loading = false;
  }

  ngOnInit(): void {
    this.loading = true;
    const finishFetching = () => this.loading = false;
    this._productService.getProducts({}).subscribe({
      next: products => {
        this.originalProducts = products;
        this.products = products;
        this.productSelections = products.map(_ => false);
      },
      error: () => {
        this._messageService.error('Error fetching products');
        finishFetching();
      },
      complete: () => finishFetching()
    });
  }

  reset(): void {
    this.searchValue = '';
    this.search();
  }

  search(): void {
    const filteredProducts = (this.originalProducts || [])
      .filter((item: ProductModel) => item.name.indexOf(this.searchValue) !== -1);
    this.productSelections = filteredProducts.map(_ => false);
    this.products = filteredProducts;
    this._saveSearchInteraction()
  }

  onSelectProduct(checked: boolean, index: number) {
    this._checkSelectedProducts();
  }

  clearSelection() {
    this.productSelections = this.productSelections.map(_ => false);
    this._checkSelectedProducts();
  }

  submitOrder() {
    this._submitCreateOrder(this.selectedProducts);
  }

  private _checkSelectedProducts() {
    this.selectedProducts = this.products?.filter((item, idx) => this.productSelections[idx]) || [];
  }

  private _submitCreateOrder(selectedProducts: ProductModel[]) {
    if (!selectedProducts.length) return;

    this.loading = true;

    const submitFinish = () => this.loading = false;

    this._orderService.submitOrder({
      productIds: selectedProducts.map(p => p.id)
    }).subscribe({
      next: () => {
        this._router.navigate(['/', 'orders']);
        this._messageService.success('Submitted order successfully');
      },
      error: (err) => {
        this._messageService.error('Failed to submit order');
        submitFinish();
      },
      complete: () => submitFinish()
    });
  }

  private _saveSearchInteraction() {
    this._interactionService.saveSearch(this.searchValue).subscribe({
      next: () => {
        console.log('Save search ', this.searchValue);
      },
      error: (err) => {
        console.log('Error: ', err);
      }
    });
  }
}
