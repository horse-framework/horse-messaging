import { NgModule, CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LayoutComponent } from './layout.component';
import { HeaderComponent } from './header/header.component';
import { FooterComponent } from './footer/footer.component';
import { RouterModule } from '@angular/router';

@NgModule({
    declarations: [
        LayoutComponent,
        HeaderComponent,
        FooterComponent
    ],
    imports: [
        CommonModule,
        RouterModule
    ],
    exports: [
        LayoutComponent
    ],
    schemas: [CUSTOM_ELEMENTS_SCHEMA]
})
export class LayoutModule { }
